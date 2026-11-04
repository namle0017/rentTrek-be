using System.Text.Json;
using Microsoft.Extensions.Logging;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Helpers;

/// <summary>Quy tắc dùng chung cho các service Trip / TripGroup: participant vận hành, đồ cấp, điều kiện chốt và mapper.</summary>
internal static class TripGroupRules
{
    /// <summary>
    /// PRD §3.2 quyền "G" (NFR-01): Staff chỉ được thao tác chuyến mình phụ trách (Trip.ManagedByUserId); Admin và các vai trò khác không bị chặn ở đây.
    /// </summary>
    public static void EnsureManagesTrip(ICurrentUserService currentUser, Trip trip)
    {
        if (currentUser.Role == RoleNames.Staff && trip.ManagedByUserId != currentUser.UserId)
            throw new ForbiddenException("Bạn không phụ trách chuyến đi này", ErrorCodes.TripNotAssigned);
    }

    internal static bool IsActivePaidOrder(Order? order)
        => order is not null
           && !order.IsDeleted
           && string.Equals(order.Status, OrderStatuses.Confirmed, StringComparison.OrdinalIgnoreCase)
           && string.Equals(order.PaymentStatus, OrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase);

    internal static bool IsOperationalParticipant(BookingParticipant participant)
        => !participant.IsDeleted
           && !string.Equals(participant.Status, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
           && IsActivePaidOrder(participant.BookingOrder?.Order);

    internal static void EnsureExpectedRowVersion(Trip trip, int? expectedTripRowVersion)
    {
        if (expectedTripRowVersion.HasValue && trip.RowVersion != expectedTripRowVersion.Value)
        {
            throw new ConflictException(
                "Dữ liệu chuyến đi đã được cập nhật bởi thao tác khác, vui lòng tải lại",
                ErrorCodes.ConcurrencyConflict);
        }
    }

    internal static async Task<int> GetGroupOverflowSlotsAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var raw = await unitOfWork.SystemConfigurations.GetValueAsync(TripGroupConfigKeys.GroupOverflowSlots, cancellationToken);
        return int.TryParse(raw, out var parsed) && parsed >= 0
            ? parsed
            : TripGroupConfigKeys.DefaultGroupOverflowSlots;
    }

    internal static Trip BuildDraftTripWithGroups(
        Tour tour,
        Guid? recurrenceRuleId,
        Guid refundPolicyId,
        Guid managedByUserId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        int totalSlots,
        int groupCount,
        decimal pricePerPerson,
        int minParticipants,
        DateTimeOffset utcNow)
    {
        var tripId = Guid.NewGuid();
        var trip = new Trip
        {
            TripId = tripId,
            TourId = tour.Id,
            Tour = tour,
            RecurrenceRuleId = recurrenceRuleId,
            RefundPolicyId = refundPolicyId,
            ManagedByUserId = managedByUserId,
            StartAt = startAt,
            EndAt = endAt,
            TotalSlots = totalSlots,
            PricePerPerson = pricePerPerson,
            MinParticipants = minParticipants,
            Status = TripStatuses.Draft,
            RowVersion = 1,
            ProvidedItemsNeedsRecalc = true,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        for (var i = 1; i <= groupCount; i++)
        {
            trip.TripGroups.Add(new TripGroup
            {
                TripGroupId = Guid.NewGuid(),
                TripId = tripId,
                Trip = trip,
                GroupName = $"Nhóm {i}",
                MaxParticipants = tour.GroupMaxParticipants!.Value,
                Status = TripGroupStatuses.Draft,
                GuideUserId = null,
                CreatedAt = utcNow.UtcDateTime.AddTicks(i - 1),
                UpdatedAt = utcNow.UtcDateTime
            });
        }

        return trip;
    }

    internal static IReadOnlyList<PublishBlockerResponse> GetPublishBlockers(Trip trip, DateTimeOffset utcNow)
    {
        var blockers = new List<PublishBlockerResponse>();
        var activeGroups = GetActiveGroups(trip).ToList();
        var activeGroupIds = activeGroups.Select(g => g.TripGroupId).ToHashSet();

        if (!string.Equals(trip.Status, TripStatuses.ClosedForBooking, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(new PublishBlockerResponse
            {
                Code = ErrorCodes.TripNotClosedForBooking,
                Message = "Trip phải ở trạng thái ClosedForBooking trước khi chốt group"
            });
        }

        if (activeGroups.Count == 0)
        {
            blockers.Add(new PublishBlockerResponse
            {
                Code = ErrorCodes.TripOperationalCapacityInsufficient,
                Message = "Trip chưa có TripGroup nào để chốt"
            });
        }

        var hasValidPaymentHold = trip.TripBookingOrders.Any(b =>
            b.Order is not null &&
            !b.Order.IsDeleted &&
            string.Equals(b.Order.Status, OrderStatuses.PendingPayment, StringComparison.OrdinalIgnoreCase) &&
            b.Order.HoldExpiresAt > utcNow);
        if (hasValidPaymentHold)
        {
            blockers.Add(new PublishBlockerResponse
            {
                Code = ErrorCodes.ActivePaymentHolds,
                Message = "Còn booking đang giữ chỗ thanh toán hợp lệ chưa hết hạn"
            });
        }

        var operationalParticipants = GetTripOperationalParticipants(trip);
        var unassignedCount = operationalParticipants.Count(p => p.TripGroupId is null || !activeGroupIds.Contains(p.TripGroupId.Value));
        if (unassignedCount > 0)
        {
            blockers.Add(new PublishBlockerResponse
            {
                Code = ErrorCodes.UnassignedParticipants,
                Message = $"Còn {unassignedCount} người chưa được xếp nhóm"
            });
        }

        if (activeGroups.Any(g => CountOperationalParticipants(trip, g.TripGroupId) > 0 && g.GuideUserId is null))
        {
            blockers.Add(new PublishBlockerResponse
            {
                Code = ErrorCodes.GroupGuideRequired,
                Message = "Mọi group có người tham gia phải được gán hướng dẫn viên"
            });
        }

        if (!AreProvidedItemsUpToDate(trip))
        {
            blockers.Add(new PublishBlockerResponse
            {
                Code = ErrorCodes.EquipmentRequirementsStale,
                Message = "Nhu cầu đồ cấp chuyến (PER_GROUP / PER_PARTICIPANT) chưa được tính lại và cập nhật"
            });
        }

        return blockers;
    }

    internal static bool AreProvidedItemsUpToDate(Trip trip)
    {
        if (trip.ProvidedItemsNeedsRecalc)
            return false;

        var tourItems = (trip.Tour?.ProvidedItems ?? [])
            .Where(i => !i.IsDeleted)
            .ToList();
        if (tourItems.Count == 0)
            return true;

        var activeGroupsWithParticipants = GetActiveGroups(trip)
            .Count(g => CountOperationalParticipants(trip, g.TripGroupId) > 0);
        var activeParticipantCount = GetTripOperationalParticipants(trip).Count;

        var byTourItem = trip.TripProvidedItems.ToDictionary(x => x.TourProvidedItemId);
        foreach (var tourItem in tourItems)
        {
            if (!byTourItem.TryGetValue(tourItem.Id, out var tripItem))
                return false;

            var expected = ComputeRequiredItemQuantity(tourItem, activeGroupsWithParticipants, activeParticipantCount);
            if (tripItem.RequiredQuantity != expected)
                return false;
        }

        return true;
    }

    internal static int ComputeRequiredItemQuantity(
        TourProvidedItem item,
        int activeGroupsWithParticipants,
        int activeParticipantCount)
    {
        var normalizedBasis = (item.QuantityBasis ?? string.Empty)
            .Trim()
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalizedBasis switch
        {
            "pergroup" => item.Quantity * activeGroupsWithParticipants,
            "perperson" or "perparticipant" => item.Quantity * activeParticipantCount,
            _ => throw new ConflictException($"QuantityBasis '{item.QuantityBasis}' của đồ cấp '{item.ItemName}' không được hỗ trợ")
        };
    }

    internal static IEnumerable<TripGroup> GetActiveGroups(Trip trip)
        => trip.TripGroups
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.TripGroupId);

    internal static List<BookingParticipant> GetTripOperationalParticipants(Trip trip)
    {
        var byId = new Dictionary<Guid, BookingParticipant>();

        foreach (var booking in trip.TripBookingOrders)
        {
            if (!IsActivePaidOrder(booking.Order))
                continue;

            foreach (var participant in booking.Participants)
            {
                if (!participant.IsDeleted && !string.Equals(participant.Status, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
                    byId[participant.ParticipantId] = participant;
            }
        }

        foreach (var group in trip.TripGroups.Where(g => !g.IsDeleted))
        {
            foreach (var participant in group.BookingParticipants.Where(IsOperationalParticipant))
            {
                byId.TryAdd(participant.ParticipantId, participant);
            }
        }

        return byId.Values.ToList();
    }

    internal static List<BookingParticipant> GetGroupOperationalParticipants(
        Trip trip,
        Guid groupId,
        Guid? excludeOrderId = null)
        => GetTripOperationalParticipants(trip)
            .Where(p => p.TripGroupId == groupId && (!excludeOrderId.HasValue || p.BookingOrderId != excludeOrderId.Value))
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.ParticipantId)
            .ToList();

    internal static int CountOperationalParticipants(
        Trip trip,
        Guid groupId,
        Guid? excludeOrderId = null)
        => GetGroupOperationalParticipants(trip, groupId, excludeOrderId).Count;

    internal static AdminTripResponse MapAdminTrip(Trip trip, int overflowSlots)
    {
        var activeGroups = GetActiveGroups(trip).ToList();
        var standardCapacity = activeGroups.Sum(g => g.MaxParticipants);
        var operationalCapacity = activeGroups.Sum(g => g.MaxParticipants + overflowSlots);
        var hasTourMax = trip.Tour?.GroupMaxParticipants is > 0;
        var canOpen = hasTourMax && activeGroups.Count > 0 && operationalCapacity >= trip.TotalSlots;

        string? warning = null;
        if (!hasTourMax)
            warning = "Tour chưa cấu hình GroupMaxParticipants dương.";
        else if (activeGroups.Count == 0)
            warning = "Trip chưa có group nào.";
        else if (operationalCapacity < trip.TotalSlots)
            warning = $"Sức chứa vận hành gồm overflow ({operationalCapacity}) nhỏ hơn TotalSlots ({trip.TotalSlots}).";

        return new AdminTripResponse
        {
            TripId = trip.TripId,
            TourId = trip.TourId,
            TourName = trip.Tour?.Name ?? string.Empty,
            TourGroupMaxParticipants = trip.Tour?.GroupMaxParticipants,
            RecurrenceRuleId = trip.RecurrenceRuleId,
            StartAt = trip.StartAt,
            EndAt = trip.EndAt,
            TotalSlots = trip.TotalSlots,
            MinParticipants = trip.MinParticipants,
            PricePerPerson = trip.PricePerPerson,
            Status = trip.Status,
            RowVersion = trip.RowVersion,
            GroupCount = activeGroups.Count,
            GroupOverflowSlots = overflowSlots,
            StandardGroupCapacity = standardCapacity,
            OperationalGroupCapacity = operationalCapacity,
            CanOpenForBooking = canOpen,
            OpenReadinessWarning = warning,
            ProvidedItemsNeedsRecalc = trip.ProvidedItemsNeedsRecalc,
            Groups = activeGroups.Select(g => new AdminTripGroupSummaryResponse
            {
                TripGroupId = g.TripGroupId,
                GroupName = g.GroupName,
                MaxParticipants = g.MaxParticipants,
                OverflowSlots = overflowSlots,
                CurrentParticipants = CountOperationalParticipants(trip, g.TripGroupId),
                Status = g.Status,
                GuideUserId = g.GuideUserId,
                GuideName = g.GuideUser?.FullName
            }).ToList()
        };
    }

    internal static TripGroupingSnapshotResponse MapGroupingSnapshot(
        Trip trip,
        int overflowSlots,
        DateTimeOffset utcNow,
        Guid? guideScopeUserId)
    {
        var allActiveGroups = GetActiveGroups(trip).ToList();
        var visibleGroups = guideScopeUserId.HasValue
            ? allActiveGroups.Where(g => g.GuideUserId == guideScopeUserId.Value).ToList()
            : allActiveGroups;

        var allActiveGroupIds = allActiveGroups.Select(g => g.TripGroupId).ToHashSet();
        var groupingStatus = allActiveGroups.Count > 0 &&
                             allActiveGroups.All(g => !string.Equals(g.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            ? TripGroupStatuses.Published
            : TripGroupStatuses.Draft;

        var groupMaxParticipants = trip.Tour?.GroupMaxParticipants
                                   ?? allActiveGroups.FirstOrDefault()?.MaxParticipants
                                   ?? 0;

        var groupCards = visibleGroups.Select(g =>
        {
            var participants = GetGroupOperationalParticipants(trip, g.TripGroupId);
            var participantCount = participants.Count;
            var remainingStandard = Math.Max(0, g.MaxParticipants - participantCount);
            var remainingOverflow = Math.Max(0, g.MaxParticipants + overflowSlots - Math.Max(g.MaxParticipants, participantCount));

            var bookings = participants
                .GroupBy(p => p.BookingOrderId)
                .Select(grp =>
                {
                    var booking = grp.First().BookingOrder
                                  ?? trip.TripBookingOrders.First(b => b.OrderId == grp.Key);
                    return new TripGroupBookingCardResponse
                    {
                        BookingOrderId = booking.OrderId,
                        OrderCode = booking.Order?.OrderCode ?? string.Empty,
                        ParticipantCount = grp.Count(),
                        IsSplit = booking.IsSplit,
                        Participants = grp
                            .OrderBy(p => p.CreatedAt)
                            .ThenBy(p => p.ParticipantId)
                            .Select(p => new TripGroupParticipantItemResponse
                            {
                                ParticipantId = p.ParticipantId,
                                FullName = p.FullName
                            })
                            .ToList()
                    };
                })
                .OrderBy(b => b.OrderCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.BookingOrderId)
                .ToList();

            return new TripGroupCardResponse
            {
                TripGroupId = g.TripGroupId,
                GroupName = g.GroupName,
                Status = g.Status,
                Guide = g.GuideUserId is null
                    ? null
                    : new TripGroupGuideSummaryResponse
                    {
                        UserId = g.GuideUserId.Value,
                        FullName = g.GuideUser?.FullName ?? string.Empty
                    },
                MaxParticipants = g.MaxParticipants,
                OverflowSlots = overflowSlots,
                ParticipantCount = participantCount,
                RemainingStandardSlots = remainingStandard,
                RemainingOverflowSlots = remainingOverflow,
                IsOverflowing = participantCount > g.MaxParticipants,
                Bookings = bookings
            };
        }).ToList();

        if (guideScopeUserId.HasValue)
        {
            return new TripGroupingSnapshotResponse
            {
                TripId = trip.TripId,
                TripStatus = trip.Status,
                GroupingStatus = groupingStatus,
                TotalSlots = trip.TotalSlots,
                GroupMaxParticipants = groupMaxParticipants,
                OverflowSlots = overflowSlots,
                StandardCapacity = visibleGroups.Sum(g => g.MaxParticipants),
                OperationalCapacity = visibleGroups.Sum(g => g.MaxParticipants + overflowSlots),
                AssignedParticipants = groupCards.Sum(c => c.ParticipantCount),
                UnassignedParticipants = 0,
                RowVersion = trip.RowVersion,
                CanPublish = false,
                PublishBlockers = [],
                Groups = groupCards,
                UnassignedBookings = [],
                ProvidedItemsNeedsRecalc = false,
                ProvidedItems = []
            };
        }

        var blockers = GetPublishBlockers(trip, utcNow);
        var unassignedBookings = trip.TripBookingOrders
            .Where(b => IsActivePaidOrder(b.Order))
            .Select(b => new
            {
                Booking = b,
                UnassignedParticipants = b.Participants
                    .Where(p => !p.IsDeleted &&
                                !string.Equals(p.Status, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                                (p.TripGroupId is null || !allActiveGroupIds.Contains(p.TripGroupId.Value)))
                    .OrderBy(p => p.CreatedAt)
                    .ThenBy(p => p.ParticipantId)
                    .ToList()
            })
            .Where(x => x.UnassignedParticipants.Count > 0)
            .OrderBy(x => x.Booking.Order.CreatedAt)
            .ThenBy(x => x.Booking.OrderId)
            .Select(x => new TripGroupBookingCardResponse
            {
                BookingOrderId = x.Booking.OrderId,
                OrderCode = x.Booking.Order.OrderCode,
                ParticipantCount = x.UnassignedParticipants.Count,
                IsSplit = x.Booking.IsSplit,
                Participants = x.UnassignedParticipants
                    .Select(p => new TripGroupParticipantItemResponse
                    {
                        ParticipantId = p.ParticipantId,
                        FullName = p.FullName
                    })
                    .ToList()
            })
            .ToList();

        var assignedTotal = groupCards.Sum(c => c.ParticipantCount);
        var unassignedTotal = unassignedBookings.Sum(b => b.ParticipantCount);

        return new TripGroupingSnapshotResponse
        {
            TripId = trip.TripId,
            TripStatus = trip.Status,
            GroupingStatus = groupingStatus,
            TotalSlots = trip.TotalSlots,
            GroupMaxParticipants = groupMaxParticipants,
            OverflowSlots = overflowSlots,
            StandardCapacity = allActiveGroups.Sum(g => g.MaxParticipants),
            OperationalCapacity = allActiveGroups.Sum(g => g.MaxParticipants + overflowSlots),
            AssignedParticipants = assignedTotal,
            UnassignedParticipants = unassignedTotal,
            RowVersion = trip.RowVersion,
            CanPublish = blockers.Count == 0 &&
                         allActiveGroups.Any(g => string.Equals(g.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase)),
            PublishBlockers = blockers,
            Groups = groupCards,
            UnassignedBookings = unassignedBookings,
            ProvidedItemsNeedsRecalc = trip.ProvidedItemsNeedsRecalc || !AreProvidedItemsUpToDate(trip),
            ProvidedItems = MapProvidedItems(trip)
        };
    }

    internal static IReadOnlyList<TripProvidedItemSummaryResponse> MapProvidedItems(Trip trip)
        => trip.TripProvidedItems
            .OrderBy(i => i.TourProvidedItem?.ItemName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(i => new TripProvidedItemSummaryResponse
            {
                TripProvidedItemId = i.TripProvidedItemId,
                TourProvidedItemId = i.TourProvidedItemId,
                ProductVariantId = i.ProductVariantId,
                ItemName = i.TourProvidedItem?.ItemName ?? string.Empty,
                QuantityBasis = i.TourProvidedItem?.QuantityBasis ?? string.Empty,
                UnitQuantity = i.TourProvidedItem?.Quantity ?? 0,
                RequiredQuantity = i.RequiredQuantity,
                Status = i.Status
            })
            .ToList();

    internal static Task AddAuditLogAsync(
        IUnitOfWork unitOfWork,
        Guid? actorUserId,
        string action,
        string entityType,
        Guid entityId,
        object details,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
        => unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = JsonSerializer.Serialize(details),
            CreatedAt = utcNow.UtcDateTime
        }, cancellationToken);

    /// <summary>Group đã chốt (khác Draft) thì không được sửa qua API thường.</summary>
    internal static void EnsureDraftGroup(TripGroup group, string message)
    {
        if (!string.Equals(group.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(message, ErrorCodes.TripGroupingLocked);
    }

    internal static bool IsValidActiveGuideAccount(Account account)
        => !account.IsDeleted
           && string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase)
           && account.Role is not null
           && (string.Equals(account.Role.RoleName, RoleNames.Guide, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(account.Role.RoleName, RoleNames.TourGuide, StringComparison.OrdinalIgnoreCase));
}
