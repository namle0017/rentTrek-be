using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using static RenTrek.BLL.Helpers.TripGroupRules;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Màn hình group của Guide và màn hình chia đoàn của Khách.</summary>
public sealed class TripGroupViewService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : ITripGroupViewService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<IReadOnlyList<GuideTripGroupResponse>> GetGuideGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        var guideUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();
        var groups = await unitOfWork.TripGroups.GetGuideGroupsAsync(guideUserId, cancellationToken);
        return groups.Select(g => MapGuideGroup(g, utcNow)).ToList();
    }

    public async Task<GuideTripGroupResponse> GetGuideGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var guideUserId = currentUser.RequiredUserId;
        var group = await unitOfWork.TripGroups.GetGuideGroupByIdAsync(groupId, guideUserId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy group được phân công", ErrorCodes.ItemNotFound);
        return MapGuideGroup(group, timeProvider.GetUtcNow());
    }

    public async Task<CustomerTripGroupingResponse> GetCustomerGroupingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var order = await unitOfWork.TripBookings.GetForAccessAsync(orderId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy booking", ErrorCodes.OrderNotFound);

        var isBuyer = order.CustomerUserId == userId;
        if (!isBuyer)
        {
            var account = await unitOfWork.TripBookings.GetAccountAsync(userId, cancellationToken);
            if (account is null ||
                account.EmailConfirmedAt is null ||
                !string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase) ||
                !order.TripBookingOrder.Participants.Any(p => p.LinkedUserId == userId))
            {
                throw new NotFoundException("Không tìm thấy booking", ErrorCodes.OrderNotFound);
            }
        }

        var booking = order.TripBookingOrder;
        if (!IsActivePaidOrder(order))
        {
            return new CustomerTripGroupingResponse
            {
                TripId = booking.TripId,
                GroupingStatus = TripGroupStatuses.Pending,
                Groups = []
            };
        }

        var authorizedParticipants = booking.Participants
            .Where(p => !p.IsDeleted &&
                        !string.Equals(p.Status, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                        (isBuyer || p.LinkedUserId == userId))
            .Where(p => p.TripGroup is not null &&
                        !p.TripGroup.IsDeleted &&
                        !string.Equals(p.TripGroup.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (authorizedParticipants.Count == 0)
        {
            return new CustomerTripGroupingResponse
            {
                TripId = booking.TripId,
                GroupingStatus = TripGroupStatuses.Pending,
                Groups = []
            };
        }

        var groups = authorizedParticipants
            .GroupBy(p => p.TripGroup!.TripGroupId)
            .Select(grp =>
            {
                var group = grp.First().TripGroup!;
                return new CustomerTripGroupResponse
                {
                    TripGroupId = group.TripGroupId,
                    GroupName = group.GroupName,
                    Guide = group.GuideUser is null
                        ? null
                        : new TripGroupGuideSummaryResponse
                        {
                            UserId = group.GuideUser.UserId,
                            FullName = group.GuideUser.FullName
                        },
                    Participants = grp
                        .OrderBy(p => p.CreatedAt)
                        .ThenBy(p => p.ParticipantId)
                        .Select(p => new CustomerGroupParticipantResponse
                        {
                            ParticipantId = p.ParticipantId,
                            FullName = p.FullName
                        })
                        .ToList()
                };
            })
            .OrderBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.TripGroupId)
            .ToList();

        return new CustomerTripGroupingResponse
        {
            TripId = booking.TripId,
            GroupingStatus = TripGroupStatuses.Published,
            Groups = groups
        };
    }

    private static GuideTripGroupResponse MapGuideGroup(TripGroup group, DateTimeOffset utcNow)
    {
        var participants = group.BookingParticipants
            .Where(IsOperationalParticipant)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.ParticipantId)
            .Select(p => new GuideGroupParticipantResponse
            {
                ParticipantId = p.ParticipantId,
                FullName = p.FullName,
                Phone = p.Phone,
                DateOfBirth = p.DateOfBirth,
                EmergencyContactName = p.EmergencyContactName,
                EmergencyContactPhone = p.EmergencyContactPhone,
                Status = p.Status
            })
            .ToList();

        return new GuideTripGroupResponse
        {
            TripGroupId = group.TripGroupId,
            TripId = group.TripId,
            TourId = group.Trip.TourId,
            TourName = group.Trip.Tour?.Name ?? string.Empty,
            GroupName = group.GroupName,
            Status = group.Status,
            TripStatus = group.Trip.Status,
            TripStartAt = group.Trip.StartAt,
            TripEndAt = group.Trip.EndAt,
            CurrentParticipants = participants.Count,
            MaxParticipants = group.MaxParticipants,
            Participants = participants,
            Activities = BuildTripActivityPreviews(group.Trip, utcNow)
        };
    }

    private static List<TripActivityPreviewResponse> BuildTripActivityPreviews(Trip trip, DateTimeOffset utcNow)
    {
        var startDateVn = trip.StartAt.ToOffset(VietnamOffset).Date;
        var isOngoing = trip.StartAt <= utcNow && utcNow < trip.EndAt;

        return (trip.Tour?.Activities ?? [])
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.DayNumber)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.StartTime)
            .Select(a =>
            {
                var dayDate = startDateVn.AddDays(Math.Max(0, a.DayNumber - 1));
                var scheduledStart = new DateTimeOffset(dayDate.Add(a.StartTime), VietnamOffset);
                var scheduledEnd = scheduledStart.AddMinutes(Math.Max(0, a.DurationMinutes));
                var isCurrent = isOngoing && utcNow >= scheduledStart &&
                                (a.DurationMinutes > 0 ? utcNow < scheduledEnd : utcNow == scheduledStart);

                return new TripActivityPreviewResponse
                {
                    ActivityId = a.Id,
                    DayNumber = a.DayNumber,
                    DayTitle = a.DayTitle,
                    StartTime = a.StartTime,
                    DurationMinutes = a.DurationMinutes,
                    DisplayOrder = a.DisplayOrder,
                    Name = a.Name,
                    Description = a.Description,
                    ScheduledStartAt = scheduledStart,
                    ScheduledEndAt = scheduledEnd,
                    IsCurrent = isCurrent
                };
            })
            .ToList();
    }
}
