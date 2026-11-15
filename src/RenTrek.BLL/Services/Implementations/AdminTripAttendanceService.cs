using System.Globalization;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ quản lý ma trận điểm danh hoạt động tour và lịch sử chỉnh sửa (Màn O14).
/// </summary>
public sealed class AdminTripAttendanceService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IAdminTripAttendanceService
{
    public async Task<TripAttendanceMatrixResponse> GetAttendanceMatrixAsync(
        Guid tripId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.ActivityAttendances.GetTripWithTourAndActivitiesAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var activities = (trip.Tour?.Activities ?? [])
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.DayNumber)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.StartTime)
            .Select(a => new AttendanceMatrixActivityColumnResponse
            {
                ActivityId = a.Id,
                DayNumber = a.DayNumber,
                Name = a.Name,
                StartTime = a.StartTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            })
            .ToList();

        var participants = await unitOfWork.ActivityAttendances.GetTripParticipantsWithGroupsAsync(
            tripId,
            groupId,
            cancellationToken);

        var attendances = await unitOfWork.ActivityAttendances.GetAttendancesByTripAsync(
            tripId,
            cancellationToken);

        var attendanceLookup = attendances
            .GroupBy(a => a.ParticipantId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(
                    a => a.TourActivityId.ToString(),
                    a => new AttendanceCellValueResponse
                    {
                        Status = a.AttendanceStatus,
                        CheckedAt = new DateTimeOffset(DateTime.SpecifyKind(a.CheckedAt, DateTimeKind.Utc)),
                        CheckedBy = a.CheckedByUser?.FullName
                    }));

        var rows = participants.Select(p => new AttendanceMatrixParticipantRowResponse
        {
            ParticipantId = p.ParticipantId,
            FullName = p.FullName,
            GroupId = p.TripGroupId,
            GroupName = p.TripGroup?.GroupName,
            Attendances = attendanceLookup.TryGetValue(p.ParticipantId, out var dict)
                ? dict
                : new Dictionary<string, AttendanceCellValueResponse>()
        }).ToList();

        return new TripAttendanceMatrixResponse
        {
            Activities = activities,
            Participants = rows
        };
    }

    public async Task<UpdateTripAttendancesResponse> UpdateAttendancesAsync(
        Guid tripId,
        UpdateTripAttendancesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActivityId == Guid.Empty)
            throw new BadRequestException("Mã hoạt động là bắt buộc", "VALIDATION_ERROR");

        if (request.Records is null || request.Records.Count == 0)
            throw new BadRequestException("Danh sách bản ghi điểm danh không được rỗng", "VALIDATION_ERROR");

        // Điểm danh do HDV ghi tại hiện trường; màn quản trị chỉ để Admin xử lý ngoại lệ (Staff chỉ xem)
        if (!string.Equals(currentUser.Role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException(
                "Chỉ hướng dẫn viên tại hiện trường mới được điểm danh; Admin xử lý ngoại lệ",
                ErrorCodes.Forbidden);
        }

        if (request.Records.Any(r => string.IsNullOrWhiteSpace(r.Reason)))
        {
            throw new BadRequestException(
                "Bắt buộc nhập lý do khi Admin điều chỉnh điểm danh",
                ErrorCodes.AttendanceReasonRequired);
        }

        var trip = await unitOfWork.ActivityAttendances.GetTripWithTourAndActivitiesAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        if (!string.Equals(trip.Status, TripStatuses.InProgress, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(trip.Status, TripStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Chỉ điều chỉnh điểm danh khi chuyến đang diễn ra hoặc đã hoàn tất",
                ErrorCodes.AttendanceNotEditable);
        }

        var activity = trip.Tour?.Activities.FirstOrDefault(a => a.Id == request.ActivityId && !a.IsDeleted)
            ?? throw new NotFoundException("Không tìm thấy hoạt động trong lịch trình của tour này");

        var tripParticipants = await unitOfWork.ActivityAttendances.GetTripParticipantsWithGroupsAsync(
            tripId,
            null,
            cancellationToken);
        var participantMap = tripParticipants.ToDictionary(p => p.ParticipantId);

        var requestedIds = request.Records.Select(r => r.ParticipantId).Distinct().ToArray();
        foreach (var pid in requestedIds)
        {
            if (!participantMap.ContainsKey(pid))
                throw new NotFoundException($"Không tìm thấy hành khách {pid} trong chuyến đi này");
        }

        var existingAttendances = await unitOfWork.ActivityAttendances.GetAttendancesForUpdateAsync(
            request.ActivityId,
            requestedIds,
            cancellationToken);
        var existingByParticipant = existingAttendances.ToDictionary(a => a.ParticipantId);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var actorUserId = currentUser.UserId ?? trip.ManagedByUserId;
        var updatedCount = 0;

        foreach (var record in request.Records)
        {
            var normalizedStatus = NormalizeAttendanceStatus(record.Status);
            var participant = participantMap[record.ParticipantId];
            var reason = record.Reason!.Trim();

            if (existingByParticipant.TryGetValue(record.ParticipantId, out var existing))
            {
                if (!string.Equals(existing.AttendanceStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    var oldStatus = existing.AttendanceStatus;
                    existing.AttendanceStatus = normalizedStatus;
                    existing.CheckedAt = utcNow;
                    existing.CheckedByUserId = actorUserId;

                    await unitOfWork.Trips.AddAuditLogAsync(new AuditLog
                    {
                        AuditLogId = Guid.NewGuid(),
                        ActorUserId = currentUser.UserId,
                        Action = "UPDATE_ATTENDANCE",
                        EntityType = "ActivityAttendance",
                        EntityId = tripId,
                        Details = $"Hoạt động '{activity.Name}' (Ngày {activity.DayNumber}): sửa điểm danh của khách '{participant.FullName}' từ '{oldStatus}' sang '{normalizedStatus}'. Lý do: {reason}",
                        CreatedAt = utcNow
                    }, cancellationToken);

                    updatedCount++;
                }
            }
            else
            {
                var created = new ActivityAttendance
                {
                    AttendanceId = Guid.NewGuid(),
                    TourActivityId = request.ActivityId,
                    ParticipantId = record.ParticipantId,
                    AttendanceStatus = normalizedStatus,
                    CheckedAt = utcNow,
                    CheckedByUserId = actorUserId,
                    CreatedAt = utcNow
                };
                await unitOfWork.ActivityAttendances.AddAsync(created, cancellationToken);
                existingByParticipant[record.ParticipantId] = created;

                await unitOfWork.Trips.AddAuditLogAsync(new AuditLog
                {
                    AuditLogId = Guid.NewGuid(),
                    ActorUserId = currentUser.UserId,
                    Action = "CREATE_ATTENDANCE",
                    EntityType = "ActivityAttendance",
                    EntityId = tripId,
                    Details = $"Hoạt động '{activity.Name}' (Ngày {activity.DayNumber}): Admin ghi điểm danh '{normalizedStatus}' cho khách '{participant.FullName}'. Lý do: {reason}",
                    CreatedAt = utcNow
                }, cancellationToken);

                updatedCount++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateTripAttendancesResponse
        {
            ActivityId = request.ActivityId,
            UpdatedCount = updatedCount
        };
    }

    public async Task<IReadOnlyList<AttendanceAuditHistoryItemResponse>> GetAttendanceHistoryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        _ = await unitOfWork.ActivityAttendances.GetTripWithTourAndActivitiesAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var logs = await unitOfWork.ActivityAttendances.GetAttendanceAuditHistoryAsync(tripId, cancellationToken);

        return logs.Select(l => new AttendanceAuditHistoryItemResponse
        {
            AuditLogId = l.AuditLogId,
            ActorUserId = l.ActorUserId,
            ActorName = l.ActorUser?.FullName ?? "Hệ thống",
            Action = l.Action,
            Details = l.Details,
            CreatedAt = l.CreatedAt
        }).ToList();
    }

    internal static string NormalizeAttendanceStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new BadRequestException("Trạng thái điểm danh là bắt buộc", "VALIDATION_ERROR");

        return status.Trim() switch
        {
            var s when s.Equals(AttendanceStatuses.Present, StringComparison.OrdinalIgnoreCase) => AttendanceStatuses.Present,
            var s when s.Equals(AttendanceStatuses.Absent, StringComparison.OrdinalIgnoreCase) => AttendanceStatuses.Absent,
            var s when s.Equals(AttendanceStatuses.Late, StringComparison.OrdinalIgnoreCase) => AttendanceStatuses.Late,
            var s when s.Equals(AttendanceStatuses.Excused, StringComparison.OrdinalIgnoreCase) => AttendanceStatuses.Excused,
            _ => throw new BadRequestException(
                "Trạng thái điểm danh chỉ chấp nhận Present, Absent, Late hoặc Excused",
                "VALIDATION_ERROR")
        };
    }
}
