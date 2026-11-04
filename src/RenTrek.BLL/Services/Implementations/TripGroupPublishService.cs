using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using static RenTrek.BLL.Helpers.TripGroupRules;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Tính lại đồ cấp theo group và chốt Published.</summary>
public sealed class TripGroupPublishService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : ITripGroupPublishService
{
    public async Task<RecalculateTripProvidedItemsResponse> RecalculateProvidedItemsAsync(
        Guid tripId,
        PublishTripGroupingRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);
            EnsureManagesTrip(currentUser, trip);

            EnsureExpectedRowVersion(trip, request?.ExpectedTripRowVersion);

            var activeGroupsWithParticipants = GetActiveGroups(trip)
                .Count(g => CountOperationalParticipants(trip, g.TripGroupId) > 0);
            var activeParticipantCount = GetTripOperationalParticipants(trip).Count;

            var tourItems = (trip.Tour?.ProvidedItems ?? [])
                .Where(i => !i.IsDeleted)
                .ToList();

            var existingByTourItem = trip.TripProvidedItems
                .ToDictionary(x => x.TourProvidedItemId);

            foreach (var tourItem in tourItems)
            {
                if (tourItem.ProductVariantId is null || tourItem.ProductVariantId.Value == Guid.Empty)
                    throw new ConflictException($"Đồ cấp '{tourItem.ItemName}' chưa gắn ProductVariantId hợp lệ trong kho");

                var requiredQuantity = ComputeRequiredItemQuantity(
                    tourItem,
                    activeGroupsWithParticipants,
                    activeParticipantCount);

                if (existingByTourItem.TryGetValue(tourItem.Id, out var existing))
                {
                    existing.RequiredQuantity = requiredQuantity;
                    existing.PreparedByUserId = actorUserId;
                    existing.ProductVariantId = tourItem.ProductVariantId.Value;
                    existing.Status = TripProvidedItemStatuses.Ready;
                }
                else
                {
                    var created = new TripProvidedItem
                    {
                        TripProvidedItemId = Guid.NewGuid(),
                        TripId = trip.TripId,
                        Trip = trip,
                        TourProvidedItemId = tourItem.Id,
                        TourProvidedItem = tourItem,
                        ProductVariantId = tourItem.ProductVariantId.Value,
                        PreparedByUserId = actorUserId,
                        RequiredQuantity = requiredQuantity,
                        Status = TripProvidedItemStatuses.Ready,
                        CreatedAt = utcNow.UtcDateTime
                    };
                    trip.TripProvidedItems.Add(created);
                    await unitOfWork.TripProvidedItems.AddAsync(created, cancellationToken);
                }
            }

            trip.ProvidedItemsNeedsRecalc = false;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "RecalculateTripProvidedItems",
                "Trip",
                trip.TripId,
                new
                {
                    activeGroupsWithParticipants,
                    activeParticipantCount,
                    itemCount = tourItems.Count
                },
                utcNow,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return new RecalculateTripProvidedItemsResponse
            {
                TripId = trip.TripId,
                RowVersion = trip.RowVersion,
                ActiveGroupCount = activeGroupsWithParticipants,
                ActiveParticipantCount = activeParticipantCount,
                ProvidedItemsNeedsRecalc = trip.ProvidedItemsNeedsRecalc,
                ProvidedItems = MapProvidedItems(trip)
            };
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<TripGroupingSnapshotResponse> PublishGroupsAsync(
        Guid tripId,
        PublishTripGroupingRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();
        var overflowSlots = await GetGroupOverflowSlotsAsync(unitOfWork, cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);
            EnsureManagesTrip(currentUser, trip);

            EnsureExpectedRowVersion(trip, request?.ExpectedTripRowVersion);

            var activeGroups = GetActiveGroups(trip).ToList();
            if (activeGroups.Count > 0 &&
                activeGroups.All(g => !string.Equals(g.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException(
                    "Các group của chuyến đi đã được chốt (Published) trước đó",
                    ErrorCodes.TripGroupingLocked);
            }

            var blockers = GetPublishBlockers(trip, utcNow);
            if (blockers.Count > 0)
            {
                throw new ConflictException(
                    string.Join("; ", blockers.Select(b => b.Message)),
                    blockers[0].Code);
            }

            foreach (var group in activeGroups)
            {
                group.Status = TripGroupStatuses.Published;
                group.UpdatedAt = utcNow.UtcDateTime;
            }

            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "PublishTripGroups",
                "Trip",
                trip.TripId,
                new
                {
                    publishedGroupCount = activeGroups.Count,
                    emptyGroupsWithoutGuide = activeGroups.Count(g => g.GuideUserId is null)
                },
                utcNow,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapGroupingSnapshot(trip, overflowSlots, utcNow, guideScopeUserId: null);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
