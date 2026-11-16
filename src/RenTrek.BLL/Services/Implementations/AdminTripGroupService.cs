using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using static RenTrek.BLL.Helpers.TripGroupRules;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ chia nhóm chuyến đi dạng Kanban, tự động ghép nhóm và duyệt tách đoàn (Màn O12).
/// </summary>
/// <remarks>
/// Logic gốc: Wee (commit 57dee34 "feat(trip-ops)"). Trùng nghiệp vụ với TripGroupingService (/trips/{tripId}/grouping).
/// Gộp (2026-11-05, Tri Tin refactor): hai luồng giữ nguyên route và response cho FE, nhưng dùng chung quy tắc trong
/// TripGroupRules: chỉ sửa group Draft (EnsureDraftGroup) và guide phải là tài khoản Guide đang hoạt động (IsValidActiveGuideAccount).
/// Mọi thao tác ghi chạy trong transaction + khóa trip, ghi audit và bật ProvidedItemsNeedsRecalc như TripGroupingService.
/// </remarks>
public sealed class AdminTripGroupService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IAdminTripGroupService
{
    public async Task<KanbanGroupsBoardResponse> GetGroupsBoardAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.TripGroups.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var groups = await unitOfWork.TripGroups.GetGroupsWithDetailsByTripAsync(tripId, cancellationToken);
        var participants = await unitOfWork.TripGroups.GetActiveParticipantsWithOrderByTripAsync(tripId, cancellationToken);

        var unassigned = participants
            .Where(p => p.TripGroupId is null)
            .Select(p => new KanbanUnassignedParticipantItemResponse
            {
                ParticipantId = p.ParticipantId,
                FullName = p.FullName,
                Phone = p.Phone,
                BookingOrderId = p.BookingOrderId,
                OrderCode = p.BookingOrder.Order.OrderCode,
                GroupingPreference = FormatGroupingPreference(p.BookingOrder.GroupingPreference),
                IsSplit = p.BookingOrder.IsSplit,
                HasAccount = p.LinkedUserId.HasValue
            })
            .ToList();

        var groupColumns = new List<KanbanGroupColumnResponse>(groups.Count);
        foreach (var group in groups)
        {
            var hasOverlap = group.GuideUserId is Guid guideUserId && guideUserId != Guid.Empty &&
                             await unitOfWork.TripGroups.HasOverlappingGuideAssignmentAsync(
                                 guideUserId,
                                 trip.TripId,
                                 trip.StartAt,
                                 trip.EndAt,
                                 cancellationToken);

            groupColumns.Add(MapGroupColumn(group, hasOverlap));
        }

        return new KanbanGroupsBoardResponse
        {
            UnassignedParticipants = unassigned,
            Groups = groupColumns
        };
    }

    public async Task<KanbanGroupColumnResponse> CreateGroupAsync(
        Guid tripId,
        CreateTripGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateGroupRequest(request.GroupName, request.MaxParticipants);
        var groupName = request.GroupName.Trim();
        KanbanGroupColumnResponse? result = null;

        await InTripLockAsync(tripId, async (trip, utcNow) =>
        {
            if (await unitOfWork.TripGroups.GroupNameExistsInTripAsync(tripId, groupName, null, cancellationToken))
                throw new ConflictException($"Tên nhóm '{groupName}' đã tồn tại trong chuyến đi này", "GROUP_NAME_ALREADY_EXISTS");

            var (guide, hasOverlap) = await ResolveGuideAsync(request.GuideUserId, trip, cancellationToken);
            var group = new TripGroup
            {
                TripGroupId = Guid.NewGuid(),
                TripId = tripId,
                GuideUserId = guide?.UserId,
                GuideUser = guide,
                GroupName = groupName,
                MaxParticipants = request.MaxParticipants,
                Status = TripGroupStatuses.Draft,
                CreatedAt = utcNow.UtcDateTime,
                UpdatedAt = utcNow.UtcDateTime
            };

            await unitOfWork.TripGroups.AddAsync(group, cancellationToken);
            await MarkChangedAsync(trip, "KanbanCreateGroup", "TripGroup", group.TripGroupId,
                new { tripId, groupName, request.MaxParticipants, guideUserId = group.GuideUserId }, utcNow, cancellationToken);
            result = MapGroupColumn(group, hasOverlap);
        }, cancellationToken);

        return result!;
    }

    public async Task<KanbanGroupColumnResponse> UpdateGroupAsync(
        Guid tripId,
        Guid groupId,
        UpdateTripGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateGroupRequest(request.GroupName, request.MaxParticipants);
        var groupName = request.GroupName.Trim();
        KanbanGroupColumnResponse? result = null;

        await InTripLockAsync(tripId, async (trip, utcNow) =>
        {
            var group = await unitOfWork.TripGroups.GetGroupForUpdateAsync(tripId, groupId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy nhóm trong chuyến đi");
            EnsureDraftGroup(group, "Nhóm đã chốt (Published), không được sửa");

            if (await unitOfWork.TripGroups.GroupNameExistsInTripAsync(tripId, groupName, groupId, cancellationToken))
                throw new ConflictException($"Tên nhóm '{groupName}' đã tồn tại trong chuyến đi này", "GROUP_NAME_ALREADY_EXISTS");

            var currentCount = group.BookingParticipants.Count(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled);
            if (request.MaxParticipants < currentCount)
            {
                throw new ConflictException(
                    $"Không thể giảm sĩ số tối đa ({request.MaxParticipants}) xuống thấp hơn số khách hiện có trong nhóm ({currentCount})",
                    "MAX_PARTICIPANTS_BELOW_CURRENT");
            }

            var (guide, hasOverlap) = await ResolveGuideAsync(request.GuideUserId, trip, cancellationToken);

            group.GroupName = groupName;
            group.MaxParticipants = request.MaxParticipants;
            if (guide is not null)
            {
                group.GuideUserId = guide.UserId;
                group.GuideUser = guide;
            }
            group.UpdatedAt = utcNow.UtcDateTime;

            await MarkChangedAsync(trip, "KanbanUpdateGroup", "TripGroup", groupId,
                new { tripId, groupName, request.MaxParticipants, guideUserId = group.GuideUserId }, utcNow, cancellationToken);
            result = MapGroupColumn(group, hasOverlap);
        }, cancellationToken);

        return result!;
    }

    public Task DeleteGroupAsync(
        Guid tripId,
        Guid groupId,
        CancellationToken cancellationToken = default)
        => InTripLockAsync(tripId, async (trip, utcNow) =>
        {
            var group = await unitOfWork.TripGroups.GetGroupForUpdateAsync(tripId, groupId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy nhóm trong chuyến đi");
            EnsureDraftGroup(group, "Nhóm đã chốt (Published), không được xóa");

            foreach (var participant in group.BookingParticipants)
            {
                participant.TripGroupId = null;
                participant.UpdatedAt = utcNow;
            }

            group.IsDeleted = true;
            group.Status = TripGroupStatuses.Dissolved;
            group.UpdatedAt = utcNow.UtcDateTime;

            await MarkChangedAsync(trip, "KanbanDeleteGroup", "TripGroup", groupId,
                new { tripId, unassignedCount = group.BookingParticipants.Count }, utcNow, cancellationToken);
        }, cancellationToken);

    public async Task<KanbanGroupsBoardResponse> AssignParticipantGroupAsync(
        Guid tripId,
        AssignParticipantGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        await InTripLockAsync(tripId, async (trip, utcNow) =>
        {
            var participant = await unitOfWork.TripGroups.GetParticipantForUpdateAsync(tripId, request.ParticipantId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy hành khách trong chuyến đi");

            var sourceGroupId = participant.TripGroupId;
            if (sourceGroupId is Guid sourceId &&
                await unitOfWork.TripGroups.GetGroupForUpdateAsync(tripId, sourceId, cancellationToken) is { } sourceGroup)
                EnsureDraftGroup(sourceGroup, "Hành khách đang thuộc nhóm đã chốt (Published), không được chuyển");

            Guid? targetGroupId = request.TargetGroupId is { } id && id != Guid.Empty ? id : null;
            if (sourceGroupId == targetGroupId)
                return;

            if (targetGroupId is Guid targetId)
            {
                var targetGroup = await unitOfWork.TripGroups.GetGroupForUpdateAsync(tripId, targetId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy nhóm đích trong chuyến đi");
                EnsureDraftGroup(targetGroup, "Nhóm đích đã chốt (Published), không được thêm hành khách");

                // Đơn đi chung (chưa duyệt tách đoàn) không được có người ở hai nhóm khác nhau.
                if (!participant.BookingOrder.IsSplit)
                {
                    var siblings = await unitOfWork.TripGroups.GetActiveParticipantsForUpdateByTripAsync(tripId, cancellationToken);
                    if (siblings.Any(p => p.BookingOrderId == participant.BookingOrderId &&
                                          p.ParticipantId != participant.ParticipantId &&
                                          p.TripGroupId is Guid g && g != targetId))
                    {
                        throw new ConflictException(
                            $"Đơn {participant.BookingOrder.Order.OrderCode} đăng ký đi chung nhóm. Cần duyệt tách đoàn trước khi xếp hành khách sang nhóm khác",
                            "BOOKING_SPLIT_NOT_APPROVED");
                    }
                }

                // Đếm trong lúc giữ khóa trip nên hai thao tác đồng thời không cùng lọt vào chỗ cuối (BR-10).
                var currentCount = await unitOfWork.TripGroups.GetGroupParticipantCountAsync(targetId, cancellationToken);
                if (currentCount >= targetGroup.MaxParticipants)
                {
                    throw new ConflictException(
                        $"Nhóm '{targetGroup.GroupName}' đã đầy sĩ số ({currentCount}/{targetGroup.MaxParticipants})",
                        ErrorCodes.GroupCapacityExceeded);
                }
            }

            participant.TripGroupId = targetGroupId;
            participant.UpdatedAt = utcNow;

            await MarkChangedAsync(trip, "KanbanMoveParticipant", "BookingParticipant", participant.ParticipantId,
                new { tripId, sourceGroupId, targetGroupId }, utcNow, cancellationToken);
        }, cancellationToken);

        return await GetGroupsBoardAsync(tripId, cancellationToken);
    }

    public async Task<AutoGroupResultResponse> AutoGroupAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var assignedCount = 0;

        await InTripLockAsync(tripId, async (trip, utcNow) =>
        {
            var groups = (await unitOfWork.TripGroups.GetGroupsForUpdateByTripAsync(tripId, cancellationToken))
                .Where(g => string.Equals(g.Status, TripGroupStatuses.Draft, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var participants = await unitOfWork.TripGroups.GetActiveParticipantsForUpdateByTripAsync(tripId, cancellationToken);

            var unassigned = participants.Where(p => p.TripGroupId is null).ToList();
            if (unassigned.Count == 0 || groups.Count == 0)
                return;

            var groupCapacities = groups.ToDictionary(
                g => g.TripGroupId,
                g => Math.Max(0, g.MaxParticipants - g.BookingParticipants.Count(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled)));

            var parties = unassigned
                .GroupBy(p => p.BookingOrderId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.First().BookingOrder.Order.CreatedAt)
                .ToList();

            foreach (var party in parties)
            {
                var members = party.ToList();
                var bookingOrder = members[0].BookingOrder;

                // 1. Ưu tiên tìm nhóm còn đủ chỗ cho cả đoàn trong cùng đơn đặt (First-Fit Decreasing)
                var fittingGroup = groups.FirstOrDefault(g => groupCapacities[g.TripGroupId] >= members.Count);
                if (fittingGroup is not null)
                {
                    foreach (var member in members)
                    {
                        member.TripGroupId = fittingGroup.TripGroupId;
                        member.UpdatedAt = utcNow;
                        assignedCount++;
                    }
                    groupCapacities[fittingGroup.TripGroupId] -= members.Count;
                    continue;
                }

                // 2. Nếu đơn đã được duyệt tách đoàn (IsSplit = true), cho phép chia lẻ vào các nhóm còn chỗ trống
                if (bookingOrder.IsSplit)
                {
                    foreach (var member in members)
                    {
                        var availableGroup = groups.FirstOrDefault(g => groupCapacities[g.TripGroupId] > 0);
                        if (availableGroup is null)
                            break;

                        member.TripGroupId = availableGroup.TripGroupId;
                        member.UpdatedAt = utcNow;
                        groupCapacities[availableGroup.TripGroupId]--;
                        assignedCount++;
                    }
                }
            }

            if (assignedCount > 0)
                await MarkChangedAsync(trip, "KanbanAutoGroup", "Trip", tripId, new { assignedCount }, utcNow, cancellationToken);
        }, cancellationToken);

        var updatedBoard = await GetGroupsBoardAsync(tripId, cancellationToken);
        return new AutoGroupResultResponse
        {
            AssignedCount = assignedCount,
            RemainingUnassignedCount = updatedBoard.UnassignedParticipants.Count,
            Board = updatedBoard
        };
    }

    public async Task<SplitApproveBookingResponse> ApproveBookingSplitAsync(
        Guid tripId,
        Guid bookingId,
        SplitApproveBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BadRequestException("Bắt buộc nhập lý do tách đoàn", "VALIDATION_ERROR");

        EnsureManagesTrip(currentUser, await unitOfWork.TripGroups.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi"));

        var booking = await unitOfWork.TripGroups.GetBookingOrderForUpdateAsync(tripId, bookingId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn đặt chỗ trong chuyến đi này");

        var utcNow = timeProvider.GetUtcNow();
        var actorUserId = currentUser.UserId;
        var reason = request.Reason.Trim();

        booking.IsSplit = true;
        booking.SplitReason = reason;
        booking.SplitApprovedByUserId = actorUserId;
        booking.SplitAt = utcNow;

        await unitOfWork.Trips.AddAuditLogAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = "SPLIT_APPROVE_BOOKING",
            EntityType = "TripBookingOrder",
            EntityId = booking.OrderId,
            Details = $"Phê duyệt tách nhóm cho đơn {booking.Order.OrderCode} trong chuyến {tripId}. Lý do: {reason}",
            CreatedAt = utcNow.UtcDateTime
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SplitApproveBookingResponse
        {
            BookingOrderId = booking.OrderId,
            IsSplit = booking.IsSplit,
            SplitReason = booking.SplitReason,
            SplitApprovedByUserId = booking.SplitApprovedByUserId,
            SplitAt = booking.SplitAt
        };
    }

    /// <summary>
    /// Chạy thao tác ghi trong transaction, giữ cùng khóa trip với TripGroupingService để không vượt sĩ số khi thao tác đồng thời (BR-10).
    /// </summary>
    private async Task InTripLockAsync(Guid tripId, Func<Trip, DateTimeOffset, Task> work, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripBookings.GetTripForUpdateAsync(tripId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi");
            EnsureManagesTrip(currentUser, trip);

            await work(trip, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Ghi audit (FR-D06), đánh dấu cần tính lại đồ cấp (FR-I01) và tăng RowVersion để snapshot bên luồng grouping biết dữ liệu đã đổi.
    /// </summary>
    private Task MarkChangedAsync(
        Trip trip,
        string action,
        string entityType,
        Guid entityId,
        object details,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        trip.ProvidedItemsNeedsRecalc = true;
        trip.UpdatedAt = utcNow;
        trip.RowVersion++;
        return AddAuditLogAsync(unitOfWork, currentUser.UserId, action, entityType, entityId, details, utcNow, cancellationToken);
    }

    private static void ValidateGroupRequest(string? groupName, int maxParticipants)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new BadRequestException("Tên nhóm là bắt buộc", "VALIDATION_ERROR");

        if (maxParticipants < 1)
            throw new BadRequestException("Sĩ số tối đa của nhóm phải lớn hơn hoặc bằng 1", "VALIDATION_ERROR");
    }

    private async Task<(Account? Guide, bool HasOverlap)> ResolveGuideAsync(
        Guid? guideUserId,
        Trip trip,
        CancellationToken cancellationToken)
    {
        if (guideUserId is not { } id || id == Guid.Empty)
            return (null, false);

        var guide = await unitOfWork.TripGroups.GetGuideWithRoleAndProfileAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản Hướng dẫn viên");
        if (!IsValidActiveGuideAccount(guide))
            throw new BadRequestException("Tài khoản được chọn không phải hướng dẫn viên đang hoạt động", ErrorCodes.ValidationError);

        // Kanban chỉ cảnh báo trùng lịch, không chặn.
        var hasOverlap = await unitOfWork.TripGroups.HasOverlappingGuideAssignmentAsync(
            guide.UserId, trip.TripId, trip.StartAt, trip.EndAt, cancellationToken);
        return (guide, hasOverlap);
    }

    private static KanbanGroupColumnResponse MapGroupColumn(TripGroup group, bool hasOverlap)
    {
        var activeParticipants = group.BookingParticipants
            .Where(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled)
            .OrderBy(p => p.BookingOrder?.Order?.OrderCode)
            .ThenBy(p => p.FullName)
            .Select(p => new KanbanGroupParticipantItemResponse
            {
                ParticipantId = p.ParticipantId,
                FullName = p.FullName,
                Phone = p.Phone,
                BookingOrderId = p.BookingOrderId,
                OrderCode = p.BookingOrder?.Order?.OrderCode ?? string.Empty
            })
            .ToList();

        return new KanbanGroupColumnResponse
        {
            GroupId = group.TripGroupId,
            GroupName = group.GroupName,
            MaxParticipants = group.MaxParticipants,
            CurrentCount = activeParticipants.Count,
            HasScheduleWarning = hasOverlap,
            ScheduleWarningMessage = hasOverlap
                ? "Hướng dẫn viên này đang được phân công ở một chuyến khác trùng khung thời gian"
                : null,
            Guide = group.GuideUser is null
                ? null
                : new KanbanGuideInfoResponse
                {
                    UserId = group.GuideUser.UserId,
                    FullName = group.GuideUser.FullName,
                    AvatarUrl = group.GuideUser.Profile?.AvatarUrl
                },
            Participants = activeParticipants
        };
    }

    private static string FormatGroupingPreference(string? raw)
        => string.Equals(raw, "KeepTogether", StringComparison.OrdinalIgnoreCase)
            ? "Đi chung nhóm với đơn"
            : (string.IsNullOrWhiteSpace(raw) ? "Đi chung nhóm với đơn" : raw);
}
