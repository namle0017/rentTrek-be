using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Mappings;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using static RenTrek.BLL.Helpers.TripGroupRules;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Tạo / sửa Trip kèm group và chuyển trạng thái Open / ClosedForBooking.</summary>
public sealed class TripService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : ITripService
{
    public async Task<AdminTripResponse> GetAdminTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: false, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);
        var overflowSlots = await GetGroupOverflowSlotsAsync(unitOfWork, cancellationToken);
        return MapAdminTrip(trip, overflowSlots);
    }

    public async Task<TripRefundPolicyResponse> GetTripRefundPolicyAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null || trip.IsDeleted || string.Equals(trip.Status, TripStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);
        var policy = await TripRefundPolicyResolver.ResolveAsync(
                unitOfWork.RefundPolicies, trip.RefundPolicyId, timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new NotFoundException("Chuyến đi chưa có chính sách hoàn tiền", ErrorCodes.NotFound);
        return RefundPolicyMapper.ToTripPolicy(policy);
    }

    public async Task<AdminTripResponse> CreateTripAsync(
        CreateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTripScheduleAndNumbers(
            request.TourId,
            request.StartAt,
            request.EndAt,
            request.TotalSlots,
            request.GroupCount,
            request.PricePerPerson,
            request.MinParticipants);

        var actorUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();
        var overflowSlots = await GetGroupOverflowSlotsAsync(unitOfWork, cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var tour = await unitOfWork.TripGroups.GetTourForWriteAsync(request.TourId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tour", ErrorCodes.ItemNotFound);

            if (request.GroupCount > 0 && (tour.GroupMaxParticipants is null or <= 0))
            {
                throw new ConflictException(
                    "Tour chưa cấu hình GroupMaxParticipants dương, không thể tạo TripGroup",
                    ErrorCodes.TripOperationalCapacityInsufficient);
            }

            var refundPolicyId = request.RefundPolicyId
                                 ?? await unitOfWork.TripGroups.GetActiveTourRefundPolicyIdAsync(cancellationToken)
                                 ?? throw new ConflictException("Không tìm thấy chính sách hoàn tiền TOUR đang hoạt động");

            var trip = BuildDraftTripWithGroups(
                tour,
                recurrenceRuleId: null,
                refundPolicyId,
                actorUserId,
                request.StartAt,
                request.EndAt,
                request.TotalSlots,
                request.GroupCount,
                request.PricePerPerson,
                request.MinParticipants,
                utcNow);

            await unitOfWork.Trips.AddAsync(trip, cancellationToken);
            if (trip.TripGroups.Count > 0)
                await unitOfWork.TripGroups.AddRangeAsync(trip.TripGroups, cancellationToken);

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "CreateTripWithGroups",
                "Trip",
                trip.TripId,
                new
                {
                    tourId = tour.Id,
                    trip.TotalSlots,
                    groupCount = trip.TripGroups.Count,
                    groupMaxParticipants = tour.GroupMaxParticipants
                },
                utcNow,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapAdminTrip(trip, overflowSlots);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AdminTripResponse> UpdateTripAsync(
        Guid tripId,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EndAt <= request.StartAt)
            throw new BadRequestException("Thời gian kết thúc phải lớn hơn thời gian khởi hành", ErrorCodes.ValidationError);
        if (request.TotalSlots <= 0)
            throw new BadRequestException("TotalSlots phải lớn hơn 0", ErrorCodes.ValidationError);
        if (request.GroupCount < 0)
            throw new BadRequestException("GroupCount không được âm", ErrorCodes.ValidationError);
        if (request.PricePerPerson < 0 || request.MinParticipants < 0)
            throw new BadRequestException("Giá hoặc số khách tối thiểu không hợp lệ", ErrorCodes.ValidationError);

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

            if (!string.Equals(trip.Status, TripStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "Không được tạo, xóa group hoặc sửa cấu hình Trip sau khi Trip đã rời trạng thái Draft",
                    ErrorCodes.TripGroupCountLocked);
            }

            var activeGroups = GetActiveGroups(trip).ToList();
            if (request.GroupCount > activeGroups.Count && (trip.Tour?.GroupMaxParticipants is null or <= 0))
            {
                throw new ConflictException(
                    "Tour chưa cấu hình GroupMaxParticipants dương, không thể thêm group",
                    ErrorCodes.TripOperationalCapacityInsufficient);
            }

            var previousGroupCount = activeGroups.Count;
            trip.StartAt = request.StartAt;
            trip.EndAt = request.EndAt;
            trip.TotalSlots = request.TotalSlots;
            trip.PricePerPerson = request.PricePerPerson;
            trip.MinParticipants = request.MinParticipants;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            if (request.GroupCount > activeGroups.Count)
            {
                var usedNames = activeGroups
                    .Select(g => g.GroupName.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var toAddCount = request.GroupCount - activeGroups.Count;
                var nextIndex = 1;
                var addedGroups = new List<TripGroup>(toAddCount);

                for (var i = 0; i < toAddCount; i++)
                {
                    while (usedNames.Contains($"Nhóm {nextIndex}"))
                        nextIndex++;

                    var groupName = $"Nhóm {nextIndex}";
                    usedNames.Add(groupName);

                    var group = new TripGroup
                    {
                        TripGroupId = Guid.NewGuid(),
                        TripId = trip.TripId,
                        Trip = trip,
                        GroupName = groupName,
                        MaxParticipants = trip.Tour!.GroupMaxParticipants!.Value,
                        Status = TripGroupStatuses.Draft,
                        GuideUserId = null,
                        CreatedAt = utcNow.UtcDateTime.AddTicks(i),
                        UpdatedAt = utcNow.UtcDateTime
                    };
                    trip.TripGroups.Add(group);
                    addedGroups.Add(group);
                }

                await unitOfWork.TripGroups.AddRangeAsync(addedGroups, cancellationToken);
                trip.ProvidedItemsNeedsRecalc = true;
            }
            else if (request.GroupCount < activeGroups.Count)
            {
                var toRemoveCount = activeGroups.Count - request.GroupCount;
                var removable = activeGroups
                    .Where(g => CountOperationalParticipants(trip, g.TripGroupId) == 0 &&
                                string.Equals(g.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(g => g.CreatedAt)
                    .ThenByDescending(g => g.TripGroupId)
                    .Take(toRemoveCount)
                    .ToList();

                if (removable.Count < toRemoveCount)
                {
                    throw new ConflictException(
                        "Không thể xóa group đã có người tham gia hoặc đã chốt",
                        ErrorCodes.TripGroupingLocked);
                }

                foreach (var group in removable)
                {
                    group.IsDeleted = true;
                    group.UpdatedAt = utcNow.UtcDateTime;
                }

                trip.ProvidedItemsNeedsRecalc = true;
            }

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "UpdateDraftTrip",
                "Trip",
                trip.TripId,
                new
                {
                    trip.TotalSlots,
                    previousGroupCount,
                    newGroupCount = request.GroupCount
                },
                utcNow,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapAdminTrip(trip, overflowSlots);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AdminTripResponse> UpdateTripStatusAsync(
        Guid tripId,
        UpdateTripStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var targetStatus = NormalizeTripStatus(request.Status);
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

            var previousStatus = trip.Status;
            if (string.Equals(previousStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return MapAdminTrip(trip, overflowSlots);
            }

            if (targetStatus == TripStatuses.Open)
            {
                if (!string.Equals(previousStatus, TripStatuses.Draft, StringComparison.OrdinalIgnoreCase))
                    throw new ConflictException($"Chỉ được mở bán Trip từ trạng thái Draft (hiện tại: {previousStatus})");

                EnsureCanOpenTrip(trip, overflowSlots);
                await TripRefundPolicyResolver.EnsureCanOpenAsync(unitOfWork.RefundPolicies, trip, utcNow, cancellationToken);
            }
            else if (targetStatus == TripStatuses.ClosedForBooking)
            {
                if (!string.Equals(previousStatus, TripStatuses.Open, StringComparison.OrdinalIgnoreCase))
                    throw new ConflictException($"Chỉ được đóng nhận khách từ trạng thái Open (hiện tại: {previousStatus})");
            }
            else if (targetStatus == TripStatuses.Draft)
            {
                throw new ConflictException(
                    "Không được đưa Trip quay lại trạng thái Draft sau khi đã mở",
                    ErrorCodes.TripGroupCountLocked);
            }
            else if (TripStatusTimeRules.Validate(previousStatus, targetStatus, trip.StartAt, trip.EndAt, utcNow) is { } timeError)
            {
                // InProgress / Completed / Cancelled: đúng thứ tự và đúng thời điểm (PRD §7.2)
                throw new ConflictException(timeError);
            }

            trip.Status = targetStatus;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "UpdateTripStatus",
                "Trip",
                trip.TripId,
                new { previousStatus, newStatus = targetStatus },
                utcNow,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapAdminTrip(trip, overflowSlots);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void EnsureCanOpenTrip(Trip trip, int overflowSlots)
    {
        if (trip.Tour?.GroupMaxParticipants is null or <= 0)
        {
            throw new ConflictException(
                "Tour chưa được cấu hình GroupMaxParticipants dương, không thể mở Trip",
                ErrorCodes.TripOperationalCapacityInsufficient);
        }

        var activeGroups = GetActiveGroups(trip).ToList();
        if (activeGroups.Count == 0)
        {
            throw new ConflictException(
                "Trip chưa có TripGroup nào, không thể chuyển sang Open",
                ErrorCodes.TripOperationalCapacityInsufficient);
        }

        var operationalCapacity = activeGroups.Sum(g => g.MaxParticipants + overflowSlots);
        if (operationalCapacity < trip.TotalSlots)
        {
            throw new ConflictException(
                $"Tổng sức chứa vận hành gồm overflow ({operationalCapacity}) nhỏ hơn TotalSlots ({trip.TotalSlots}), chưa thể mở Trip",
                ErrorCodes.TripOperationalCapacityInsufficient);
        }
    }

    private static string NormalizeTripStatus(string? status)
        => (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "draft" => TripStatuses.Draft,
            "open" => TripStatuses.Open,
            "closedforbooking" or "closed_for_booking" => TripStatuses.ClosedForBooking,
            "inprogress" or "in_progress" => TripStatuses.InProgress,
            "completed" => TripStatuses.Completed,
            "cancelled" => TripStatuses.Cancelled,
            _ => throw new BadRequestException("Trạng thái Trip không hợp lệ", ErrorCodes.ValidationError)
        };

    private static void ValidateTripScheduleAndNumbers(
        Guid tourId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        int totalSlots,
        int groupCount,
        decimal pricePerPerson,
        int minParticipants)
    {
        if (tourId == Guid.Empty)
            throw new BadRequestException("TourId là bắt buộc", ErrorCodes.ValidationError);
        if (endAt <= startAt)
            throw new BadRequestException("Thời gian kết thúc phải lớn hơn thời gian khởi hành", ErrorCodes.ValidationError);
        if (totalSlots <= 0)
            throw new BadRequestException("TotalSlots phải lớn hơn 0", ErrorCodes.ValidationError);
        if (groupCount < 0)
            throw new BadRequestException("GroupCount không được âm", ErrorCodes.ValidationError);
        if (pricePerPerson < 0 || minParticipants < 0)
            throw new BadRequestException("Giá hoặc số khách tối thiểu không hợp lệ", ErrorCodes.ValidationError);
    }
}
