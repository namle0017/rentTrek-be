using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.UnitOfWorks;
using static RenTrek.BLL.Helpers.TripGroupRules;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Tự xếp booking vào group sau thanh toán và điều phối group (snapshot, gán guide, chuyển, tách booking).</summary>
/// <remarks>Trùng nghiệp vụ với AdminTripGroupService (Kanban /admin/trips/{tripId}/groups*, do Wee viết, commit 57dee34); quy tắc chung nằm trong TripGroupRules.</remarks>
public sealed class TripGroupingService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : ITripGroupingService
{
    public async Task AutoAssignPaidBookingInTransactionAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var tripId = await unitOfWork.TripGroups.GetBookingTripIdAsync(orderId, cancellationToken);
        if (tripId is null)
            return;

        await unitOfWork.TripGroups.AcquireTripLockAsync(tripId.Value, cancellationToken);

        var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId.Value, asTracking: true, cancellationToken);
        if (trip is null)
            return;

        var booking = trip.TripBookingOrders.SingleOrDefault(b => b.OrderId == orderId);
        var order = booking?.Order;
        if (booking is null || !IsActivePaidOrder(order))
            return;

        var bookingParticipants = booking.Participants
            .Where(p => !p.IsDeleted && !string.Equals(p.Status, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (bookingParticipants.Count == 0)
            return;

        // Idempotent: nếu booking đã được xếp group thì không xếp lại
        if (bookingParticipants.Any(p => p.TripGroupId is not null))
            return;

        var utcNow = timeProvider.GetUtcNow();
        var chosenGroup = GetActiveGroups(trip)
            .Where(g => string.Equals(g.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            .Select(g => new
            {
                Group = g,
                CurrentParticipants = CountOperationalParticipants(trip, g.TripGroupId, excludeOrderId: orderId)
            })
            .Where(x => x.CurrentParticipants + bookingParticipants.Count <= x.Group.MaxParticipants)
            .OrderBy(x => x.CurrentParticipants)
            .ThenBy(x => x.Group.CreatedAt)
            .ThenBy(x => x.Group.TripGroupId)
            .Select(x => x.Group)
            .FirstOrDefault();

        if (chosenGroup is not null)
        {
            foreach (var participant in bookingParticipants)
            {
                participant.TripGroupId = chosenGroup.TripGroupId;
                participant.TripGroup = chosenGroup;
                participant.UpdatedAt = utcNow;
                if (chosenGroup.BookingParticipants.All(x => x.ParticipantId != participant.ParticipantId))
                    chosenGroup.BookingParticipants.Add(participant);
            }

            trip.ProvidedItemsNeedsRecalc = true;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId: null,
                action: "AutoAssignBookingToGroup",
                entityType: "TripBookingOrder",
                entityId: orderId,
                details: new
                {
                    tripId = trip.TripId,
                    tripGroupId = chosenGroup.TripGroupId,
                    groupName = chosenGroup.GroupName,
                    participantCount = bookingParticipants.Count
                },
                utcNow,
                cancellationToken);
        }
        else
        {
            trip.ProvidedItemsNeedsRecalc = true;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId: null,
                action: "BookingQueuedUnassigned",
                entityType: "TripBookingOrder",
                entityId: orderId,
                details: new
                {
                    tripId = trip.TripId,
                    participantCount = bookingParticipants.Count,
                    reason = "NoDraftGroupWithStandardCapacity"
                },
                utcNow,
                cancellationToken);
        }
    }

    public async Task<TripGroupingSnapshotResponse> GetGroupingSnapshotAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var guideScopeUserId = await ResolveGuideScopeUserIdAsync(cancellationToken);
        var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: false, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);

        if (guideScopeUserId.HasValue && !GetActiveGroups(trip).Any(g => g.GuideUserId == guideScopeUserId.Value))
            throw new NotFoundException("Không tìm thấy nhóm được phân công trong chuyến đi này", ErrorCodes.ItemNotFound);

        var overflowSlots = await GetGroupOverflowSlotsAsync(unitOfWork, cancellationToken);
        return MapGroupingSnapshot(trip, overflowSlots, timeProvider.GetUtcNow(), guideScopeUserId);
    }

    public async Task<TripGroupingSnapshotResponse> AssignGuideAsync(
        Guid tripId,
        Guid groupId,
        AssignTripGroupGuideRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();
        var overflowSlots = await GetGroupOverflowSlotsAsync(unitOfWork, cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            if (request.GuideUserId.HasValue)
            {
                if (request.GuideUserId.Value == Guid.Empty)
                    throw new BadRequestException("GuideUserId không hợp lệ", ErrorCodes.ValidationError);
                await unitOfWork.TripGroups.AcquireGuideLockAsync(request.GuideUserId.Value, cancellationToken);
            }

            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);
            EnsureManagesTrip(currentUser, trip);

            EnsureExpectedRowVersion(trip, request.ExpectedTripRowVersion);

            var group = GetActiveGroups(trip).SingleOrDefault(g => g.TripGroupId == groupId)
                ?? throw new BadRequestException("Group không thuộc chuyến đi này", ErrorCodes.GroupWrongTrip);

            EnsureDraftGroup(group, "Group đã chốt (Published), không được đổi hướng dẫn viên qua API thường");

            var previousGuideId = group.GuideUserId;
            if (request.GuideUserId is null)
            {
                group.GuideUserId = null;
                group.GuideUser = null;
                group.UpdatedAt = utcNow.UtcDateTime;
            }
            else
            {
                var guideId = request.GuideUserId.Value;
                var guideAccount = await unitOfWork.TripGroups.GetAccountWithRoleAsync(guideId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy tài khoản hướng dẫn viên", ErrorCodes.ItemNotFound);

                if (!IsValidActiveGuideAccount(guideAccount))
                    throw new BadRequestException("Tài khoản được chọn không phải hướng dẫn viên đang hoạt động", ErrorCodes.ValidationError);

                if (GetActiveGroups(trip).Any(g => g.TripGroupId != groupId && g.GuideUserId == guideId))
                {
                    throw new ConflictException(
                        "Một hướng dẫn viên chỉ được phụ trách một group trong cùng Trip",
                        ErrorCodes.GuideScheduleConflict);
                }

                if (await unitOfWork.TripGroups.HasOverlappingGuideAssignmentAsync(
                        guideId,
                        trip.TripId,
                        trip.StartAt,
                        trip.EndAt,
                        cancellationToken))
                {
                    throw new ConflictException(
                        "Hướng dẫn viên bị trùng lịch với một group thuộc Trip khác có thời gian giao nhau",
                        ErrorCodes.GuideScheduleConflict);
                }

                group.GuideUserId = guideId;
                group.GuideUser = guideAccount;
                group.UpdatedAt = utcNow.UtcDateTime;
            }

            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "AssignGuideToTripGroup",
                "TripGroup",
                group.TripGroupId,
                new
                {
                    tripId = trip.TripId,
                    previousGuideUserId = previousGuideId,
                    guideUserId = group.GuideUserId
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

    public async Task<TripGroupingSnapshotResponse> MoveBookingAsync(
        Guid tripId,
        Guid bookingOrderId,
        MoveBookingGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (bookingOrderId == Guid.Empty || request.TargetGroupId == Guid.Empty)
            throw new BadRequestException("BookingOrderId và TargetGroupId là bắt buộc", ErrorCodes.ValidationError);

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

            EnsureExpectedRowVersion(trip, request.ExpectedTripRowVersion);

            var activeGroups = GetActiveGroups(trip).ToDictionary(g => g.TripGroupId);
            if (!activeGroups.TryGetValue(request.TargetGroupId, out var targetGroup))
                throw new BadRequestException("Group đích không thuộc cùng Trip", ErrorCodes.GroupWrongTrip);

            EnsureDraftGroup(targetGroup, "Group đích đã chốt (Published), không được chuyển booking bằng API thường");

            var booking = trip.TripBookingOrders.SingleOrDefault(b => b.OrderId == bookingOrderId)
                ?? throw new BadRequestException("Booking không thuộc cùng Trip", ErrorCodes.GroupWrongTrip);

            if (!IsActivePaidOrder(booking.Order))
                throw new ConflictException("Chỉ được chuyển booking đang ở trạng thái Confirmed và Paid", ErrorCodes.OrderNotPayable);

            var participants = booking.Participants.Where(IsOperationalParticipant).ToList();
            if (participants.Count == 0)
                throw new ConflictException("Booking không có người tham gia hợp lệ để xếp nhóm");

            var sourceGroupIds = participants
                .Where(p => p.TripGroupId.HasValue)
                .Select(p => p.TripGroupId!.Value)
                .Distinct()
                .ToList();

            if (sourceGroupIds.Any(id => activeGroups.TryGetValue(id, out var src) &&
                                         !string.Equals(src.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException(
                    "Booking đang thuộc group đã chốt (Published), không được chuyển bằng API thường",
                    ErrorCodes.TripGroupingLocked);
            }

            var currentTargetCount = CountOperationalParticipants(trip, targetGroup.TripGroupId, excludeOrderId: booking.OrderId);
            var maxWithOverflow = targetGroup.MaxParticipants + overflowSlots;
            if (currentTargetCount + participants.Count > maxWithOverflow)
            {
                throw new ConflictException(
                    $"Số người vượt sức chứa tối đa gồm overflow của {targetGroup.GroupName} ({currentTargetCount + participants.Count}/{maxWithOverflow})",
                    ErrorCodes.GroupCapacityExceeded);
            }

            foreach (var participant in participants)
            {
                if (participant.TripGroupId.HasValue &&
                    activeGroups.TryGetValue(participant.TripGroupId.Value, out var oldGroup) &&
                    oldGroup.TripGroupId != targetGroup.TripGroupId)
                {
                    var tracked = oldGroup.BookingParticipants.FirstOrDefault(x => x.ParticipantId == participant.ParticipantId);
                    if (tracked is not null)
                        oldGroup.BookingParticipants.Remove(tracked);
                }

                participant.TripGroupId = targetGroup.TripGroupId;
                participant.TripGroup = targetGroup;
                participant.UpdatedAt = utcNow;
                if (targetGroup.BookingParticipants.All(x => x.ParticipantId != participant.ParticipantId))
                    targetGroup.BookingParticipants.Add(participant);
            }

            if (booking.IsSplit)
            {
                booking.IsSplit = false;
                booking.SplitReason = null;
                booking.SplitApprovedByUserId = null;
                booking.SplitAt = null;
            }

            trip.ProvidedItemsNeedsRecalc = true;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "MoveBookingToGroup",
                "TripBookingOrder",
                booking.OrderId,
                new
                {
                    tripId = trip.TripId,
                    sourceGroupIds,
                    targetGroupId = targetGroup.TripGroupId,
                    participantCount = participants.Count,
                    note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
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

    public async Task<TripGroupingSnapshotResponse> SplitBookingAsync(
        Guid tripId,
        Guid bookingOrderId,
        SplitTripBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (bookingOrderId == Guid.Empty)
            throw new BadRequestException("BookingOrderId là bắt buộc", ErrorCodes.ValidationError);

        var splitReason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(splitReason))
            throw new BadRequestException("Tách booking bắt buộc phải có lý do (reason)", ErrorCodes.ValidationError);
        if (splitReason.Length > 500)
            throw new BadRequestException("Lý do tách booking không vượt quá 500 ký tự", ErrorCodes.ValidationError);

        if (currentUser.UserId is not { } actorUserId || actorUserId == Guid.Empty)
            throw new BadRequestException("Thiếu thông tin người duyệt tách booking (SplitApprovedByUserId)", ErrorCodes.ValidationError);

        var approver = await unitOfWork.TripGroups.GetAccountWithRoleAsync(actorUserId, cancellationToken);
        if (approver is null || !string.Equals(approver.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Tài khoản duyệt tách booking không hợp lệ hoặc không hoạt động", ErrorCodes.ValidationError);

        if (request.Assignments is null || request.Assignments.Count == 0)
            throw new BadRequestException("Danh sách phân bổ (assignments) là bắt buộc", ErrorCodes.ValidationError);

        if (request.Assignments.Any(a => a.TripGroupId == Guid.Empty || a.ParticipantIds is null || a.ParticipantIds.Count == 0 || a.ParticipantIds.Any(id => id == Guid.Empty)))
            throw new BadRequestException("TripGroupId và ParticipantIds trong phân bổ không hợp lệ", ErrorCodes.ValidationError);

        var flatAssignments = request.Assignments
            .SelectMany(a => a.ParticipantIds.Select(pid => (ParticipantId: pid, TargetGroupId: a.TripGroupId)))
            .ToList();

        if (flatAssignments.GroupBy(x => x.ParticipantId).Any(g => g.Count() > 1))
            throw new BadRequestException("Mỗi participant chỉ được xuất hiện một lần khi tách booking", ErrorCodes.ValidationError);

        var distinctTargetGroupIds = flatAssignments
            .Select(x => x.TargetGroupId)
            .Distinct()
            .ToList();
        if (distinctTargetGroupIds.Count < 2)
            throw new BadRequestException("Tách booking phải phân bổ sang ít nhất 2 group khác nhau; nếu vào 1 group hãy dùng chuyển booking", ErrorCodes.ValidationError);

        var utcNow = timeProvider.GetUtcNow();
        var overflowSlots = await GetGroupOverflowSlotsAsync(unitOfWork, cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);
            EnsureManagesTrip(currentUser, trip);

            EnsureExpectedRowVersion(trip, request.ExpectedTripRowVersion);

            var activeGroups = GetActiveGroups(trip).ToDictionary(g => g.TripGroupId);
            var booking = trip.TripBookingOrders.SingleOrDefault(b => b.OrderId == bookingOrderId)
                ?? throw new BadRequestException("Booking không thuộc cùng Trip", ErrorCodes.GroupWrongTrip);

            if (!IsActivePaidOrder(booking.Order))
                throw new ConflictException("Chỉ được tách booking đang ở trạng thái Confirmed và Paid", ErrorCodes.OrderNotPayable);

            var operationalParticipants = booking.Participants.Where(IsOperationalParticipant).ToDictionary(p => p.ParticipantId);
            if (flatAssignments.Any(a => !activeGroups.ContainsKey(a.TargetGroupId) || !operationalParticipants.ContainsKey(a.ParticipantId)))
            {
                throw new BadRequestException(
                    "Group hoặc participant không thuộc cùng Trip / booking này",
                    ErrorCodes.GroupWrongTrip);
            }

            if (operationalParticipants.Count != flatAssignments.Count)
            {
                throw new BadRequestException(
                    "Mỗi participant hợp lệ của booking phải xuất hiện đúng một lần",
                    ErrorCodes.ValidationError);
            }

            var currentGroupIds = operationalParticipants.Values
                .Where(p => p.TripGroupId.HasValue)
                .Select(p => p.TripGroupId!.Value)
                .Distinct();

            if (currentGroupIds.Any(id => activeGroups.TryGetValue(id, out var src) &&
                                          !string.Equals(src.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException(
                    "Booking đang có người thuộc group đã chốt (Published), không được tách bằng API thường",
                    ErrorCodes.TripGroupingLocked);
            }

            foreach (var groupCountPair in flatAssignments.GroupBy(a => a.TargetGroupId))
            {
                var targetGroup = activeGroups[groupCountPair.Key];
                EnsureDraftGroup(targetGroup, "Group đích đã chốt (Published), không được tách booking vào group này bằng API thường");

                var existingOthers = CountOperationalParticipants(trip, targetGroup.TripGroupId, excludeOrderId: booking.OrderId);
                var maxWithOverflow = targetGroup.MaxParticipants + overflowSlots;
                if (existingOthers + groupCountPair.Count() > maxWithOverflow)
                {
                    throw new ConflictException(
                        $"Số người sau khi tách vượt sức chứa tối đa gồm overflow của {targetGroup.GroupName} ({existingOthers + groupCountPair.Count()}/{maxWithOverflow})",
                        ErrorCodes.GroupCapacityExceeded);
                }
            }

            foreach (var assignment in flatAssignments)
            {
                var participant = operationalParticipants[assignment.ParticipantId];
                var targetGroup = activeGroups[assignment.TargetGroupId];

                if (participant.TripGroupId.HasValue &&
                    activeGroups.TryGetValue(participant.TripGroupId.Value, out var oldGroup) &&
                    oldGroup.TripGroupId != targetGroup.TripGroupId)
                {
                    var tracked = oldGroup.BookingParticipants.FirstOrDefault(x => x.ParticipantId == participant.ParticipantId);
                    if (tracked is not null)
                        oldGroup.BookingParticipants.Remove(tracked);
                }

                participant.TripGroupId = targetGroup.TripGroupId;
                participant.TripGroup = targetGroup;
                participant.UpdatedAt = utcNow;
                if (targetGroup.BookingParticipants.All(x => x.ParticipantId != participant.ParticipantId))
                    targetGroup.BookingParticipants.Add(participant);
            }

            booking.IsSplit = true;
            booking.SplitReason = splitReason;
            booking.SplitApprovedByUserId = actorUserId;
            booking.SplitAt = utcNow;

            trip.ProvidedItemsNeedsRecalc = true;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
                unitOfWork,
                actorUserId,
                "SplitBookingAcrossGroups",
                "TripBookingOrder",
                booking.OrderId,
                new
                {
                    tripId = trip.TripId,
                    isSplit = booking.IsSplit,
                    splitReason = booking.SplitReason,
                    splitApprovedByUserId = booking.SplitApprovedByUserId,
                    splitAt = booking.SplitAt,
                    assignments = request.Assignments
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

    private async Task<Guid?> ResolveGuideScopeUserIdAsync(CancellationToken cancellationToken)
    {
        var role = currentUser.Role;
        if (string.Equals(role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, RoleNames.Staff, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var userId = currentUser.RequiredUserId;
        if (string.Equals(role, RoleNames.Guide, StringComparison.OrdinalIgnoreCase))
        {
            return userId;
        }

        var account = await unitOfWork.TripGroups.GetAccountWithRoleAsync(userId, cancellationToken);
        var accountRole = account?.Role?.RoleName;
        if (string.Equals(accountRole, RoleNames.Guide, StringComparison.OrdinalIgnoreCase))
        {
            return userId;
        }

        if (string.Equals(accountRole, RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(accountRole, RoleNames.Staff, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        throw new ForbiddenException("Bạn không có quyền truy cập thông tin chia đoàn", ErrorCodes.Forbidden);
    }
}
