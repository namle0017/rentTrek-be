using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ chia nhóm chuyến đi dạng Kanban, tự động ghép nhóm và duyệt tách đoàn (Màn O12).
/// </summary>
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
        if (string.IsNullOrWhiteSpace(request.GroupName))
            throw new BadRequestException("Tên nhóm là bắt buộc", "VALIDATION_ERROR");

        if (request.MaxParticipants < 1)
            throw new BadRequestException("Sĩ số tối đa của nhóm phải lớn hơn hoặc bằng 1", "VALIDATION_ERROR");

        var trip = await unitOfWork.TripGroups.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var groupName = request.GroupName.Trim();
        var exists = await unitOfWork.TripGroups.GroupNameExistsInTripAsync(tripId, groupName, null, cancellationToken);
        if (exists)
            throw new ConflictException($"Tên nhóm '{groupName}' đã tồn tại trong chuyến đi này", "GROUP_NAME_ALREADY_EXISTS");

        Account? guide = null;
        var hasOverlap = false;
        if (request.GuideUserId.HasValue && request.GuideUserId.Value != Guid.Empty)
        {
            guide = await unitOfWork.TripGroups.GetGuideWithRoleAndProfileAsync(request.GuideUserId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản Hướng dẫn viên");

            hasOverlap = await unitOfWork.TripGroups.HasOverlappingGuideAssignmentAsync(
                guide.UserId,
                trip.TripId,
                trip.StartAt,
                trip.EndAt,
                cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var group = new TripGroup
        {
            TripGroupId = Guid.NewGuid(),
            TripId = tripId,
            GuideUserId = guide?.UserId ?? Guid.Empty,
            GuideUser = guide!,
            GroupName = groupName,
            MaxParticipants = request.MaxParticipants,
            Status = "Open",
            CreatedAt = now,
            UpdatedAt = now
        };

        await unitOfWork.TripGroups.AddAsync(group, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapGroupColumn(group, hasOverlap);
    }

    public async Task<KanbanGroupColumnResponse> UpdateGroupAsync(
        Guid tripId,
        Guid groupId,
        UpdateTripGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.GroupName))
            throw new BadRequestException("Tên nhóm là bắt buộc", "VALIDATION_ERROR");

        if (request.MaxParticipants < 1)
            throw new BadRequestException("Sĩ số tối đa của nhóm phải lớn hơn hoặc bằng 1", "VALIDATION_ERROR");

        var trip = await unitOfWork.TripGroups.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var group = await unitOfWork.TripGroups.GetGroupForUpdateAsync(tripId, groupId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm trong chuyến đi");

        var groupName = request.GroupName.Trim();
        var exists = await unitOfWork.TripGroups.GroupNameExistsInTripAsync(tripId, groupName, groupId, cancellationToken);
        if (exists)
            throw new ConflictException($"Tên nhóm '{groupName}' đã tồn tại trong chuyến đi này", "GROUP_NAME_ALREADY_EXISTS");

        var currentCount = group.BookingParticipants.Count(p => !p.IsDeleted && p.Status != "Cancelled");
        if (request.MaxParticipants < currentCount)
        {
            throw new ConflictException(
                $"Không thể giảm sĩ số tối đa ({request.MaxParticipants}) xuống thấp hơn số khách hiện có trong nhóm ({currentCount})",
                "MAX_PARTICIPANTS_BELOW_CURRENT");
        }

        Account? guide = null;
        var hasOverlap = false;
        if (request.GuideUserId.HasValue && request.GuideUserId.Value != Guid.Empty)
        {
            guide = await unitOfWork.TripGroups.GetGuideWithRoleAndProfileAsync(request.GuideUserId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản Hướng dẫn viên");

            hasOverlap = await unitOfWork.TripGroups.HasOverlappingGuideAssignmentAsync(
                guide.UserId,
                trip.TripId,
                trip.StartAt,
                trip.EndAt,
                cancellationToken);
        }

        group.GroupName = groupName;
        group.MaxParticipants = request.MaxParticipants;
        if (guide is not null)
        {
            group.GuideUserId = guide.UserId;
            group.GuideUser = guide;
        }
        group.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapGroupColumn(group, hasOverlap);
    }

    public async Task DeleteGroupAsync(
        Guid tripId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await unitOfWork.TripGroups.GetGroupForUpdateAsync(tripId, groupId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm trong chuyến đi");

        var utcNow = timeProvider.GetUtcNow();
        foreach (var participant in group.BookingParticipants)
        {
            participant.TripGroupId = null;
            participant.UpdatedAt = utcNow;
        }

        group.IsDeleted = true;
        group.Status = "Dissolved";
        group.UpdatedAt = utcNow.UtcDateTime;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<KanbanGroupsBoardResponse> AssignParticipantGroupAsync(
        Guid tripId,
        AssignParticipantGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var participant = await unitOfWork.TripGroups.GetParticipantForUpdateAsync(
            tripId,
            request.ParticipantId,
            cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hành khách trong chuyến đi");

        var utcNow = timeProvider.GetUtcNow();

        if (!request.TargetGroupId.HasValue || request.TargetGroupId.Value == Guid.Empty)
        {
            participant.TripGroupId = null;
            participant.UpdatedAt = utcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetGroupsBoardAsync(tripId, cancellationToken);
        }

        var targetGroupId = request.TargetGroupId.Value;
        if (participant.TripGroupId == targetGroupId)
            return await GetGroupsBoardAsync(tripId, cancellationToken);

        var targetGroup = await unitOfWork.TripGroups.GetGroupForUpdateAsync(tripId, targetGroupId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm đích trong chuyến đi");

        var currentCount = await unitOfWork.TripGroups.GetGroupParticipantCountAsync(targetGroupId, cancellationToken);
        if (currentCount >= targetGroup.MaxParticipants)
        {
            throw new ConflictException(
                $"Nhóm '{targetGroup.GroupName}' đã đầy sĩ số ({currentCount}/{targetGroup.MaxParticipants})",
                "GROUP_CAPACITY_EXCEEDED");
        }

        participant.TripGroupId = targetGroupId;
        participant.UpdatedAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetGroupsBoardAsync(tripId, cancellationToken);
    }

    public async Task<AutoGroupResultResponse> AutoGroupAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        _ = await unitOfWork.TripGroups.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var groups = await unitOfWork.TripGroups.GetGroupsForUpdateByTripAsync(tripId, cancellationToken);
        var participants = await unitOfWork.TripGroups.GetActiveParticipantsForUpdateByTripAsync(tripId, cancellationToken);

        var unassigned = participants.Where(p => p.TripGroupId is null).ToList();
        if (unassigned.Count == 0 || groups.Count == 0)
        {
            var currentBoard = await GetGroupsBoardAsync(tripId, cancellationToken);
            return new AutoGroupResultResponse
            {
                AssignedCount = 0,
                RemainingUnassignedCount = unassigned.Count,
                Board = currentBoard
            };
        }

        var groupCapacities = groups.ToDictionary(
            g => g.TripGroupId,
            g => Math.Max(0, g.MaxParticipants - g.BookingParticipants.Count(p => !p.IsDeleted && p.Status != "Cancelled")));

        var parties = unassigned
            .GroupBy(p => p.BookingOrderId)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.First().BookingOrder.Order.CreatedAt)
            .ToList();

        var utcNow = timeProvider.GetUtcNow();
        var assignedCount = 0;

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
            await unitOfWork.SaveChangesAsync(cancellationToken);

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

    private static KanbanGroupColumnResponse MapGroupColumn(TripGroup group, bool hasOverlap)
    {
        var activeParticipants = group.BookingParticipants
            .Where(p => !p.IsDeleted && p.Status != "Cancelled")
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
