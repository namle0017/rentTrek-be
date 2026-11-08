using System.Globalization;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Guide.Requests;
using RenTrek.BLL.DTOs.Guide.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ khu vực Hướng dẫn viên thực địa (Màn G01 – G06),
/// đảm bảo phân quyền theo nhóm phụ trách và ẩn hoàn toàn dữ liệu tài chính.
/// </summary>
public sealed class GuidePortalService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IGuidePortalService
{
    public async Task<IReadOnlyList<GuideTripCardResponse>> GetMyTripsAsync(
        string? tab = "today",
        CancellationToken cancellationToken = default)
    {
        var guideUserId = currentUser.RequiredUserId;
        var assignedGroups = await unitOfWork.TripGroups.GetGuideAssignedGroupsWithTripAndLocationAsync(
            guideUserId,
            cancellationToken);

        var utcNow = timeProvider.GetUtcNow();
        var todayDate = DateOnly.FromDateTime(utcNow.UtcDateTime);
        var normalizedTab = string.IsNullOrWhiteSpace(tab) ? "today" : tab.Trim().ToLowerInvariant();

        var filtered = assignedGroups.Where(g =>
        {
            var trip = g.Trip;
            var startDate = DateOnly.FromDateTime(trip.StartAt.UtcDateTime);
            var endDate = DateOnly.FromDateTime(trip.EndAt.UtcDateTime);

            return normalizedTab switch
            {
                "today" => string.Equals(trip.Status, TripStatuses.InProgress, StringComparison.OrdinalIgnoreCase)
                           || (startDate <= todayDate && endDate >= todayDate && !string.Equals(trip.Status, TripStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
                "upcoming" => startDate > todayDate
                              && !string.Equals(trip.Status, TripStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                              && !string.Equals(trip.Status, TripStatuses.InProgress, StringComparison.OrdinalIgnoreCase),
                "completed" => string.Equals(trip.Status, TripStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                               || (endDate < todayDate && !string.Equals(trip.Status, TripStatuses.InProgress, StringComparison.OrdinalIgnoreCase)),
                "all" => true,
                _ => true
            };
        });

        return filtered.Select(g =>
        {
            var location = g.Trip.Tour?.MainLocation;
            var activeCount = g.BookingParticipants.Count(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled);

            return new GuideTripCardResponse
            {
                TripId = g.TripId,
                TourName = g.Trip.Tour?.Name ?? string.Empty,
                GroupId = g.TripGroupId,
                GroupName = g.GroupName,
                StartAt = g.Trip.StartAt,
                EndAt = g.Trip.EndAt,
                TripStatus = g.Trip.Status,
                ParticipantCount = activeCount,
                MaxParticipants = g.MaxParticipants,
                MeetingLocation = new GuideMeetingLocationResponse
                {
                    Name = location?.Name ?? string.Empty,
                    Address = location?.Address ?? string.Empty,
                    Latitude = location?.Latitude ?? 0m,
                    Longitude = location?.Longitude ?? 0m
                }
            };
        }).ToList();
    }

    public async Task<GuideGroupRosterResponse> GetGroupRosterAsync(
        Guid tripId,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var group = await GetRequiredGuideGroupAsync(tripId, cancellationToken);
        var utcNow = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(utcNow.UtcDateTime);

        var activities = (group.Trip.Tour?.Activities ?? [])
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.DayNumber)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.StartTime)
            .ToList();

        var currentActivity = DetermineCurrentActivity(group.Trip, activities, utcNow);
        var attendances = await unitOfWork.ActivityAttendances.GetAttendancesByTripAsync(tripId, cancellationToken);

        var currentActivityAttendances = currentActivity is null
            ? new Dictionary<Guid, string>()
            : attendances
                .Where(a => a.TourActivityId == currentActivity.Id)
                .ToDictionary(a => a.ParticipantId, a => a.AttendanceStatus);

        var participantsQuery = group.BookingParticipants
            .Where(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            participantsQuery = participantsQuery.Where(p =>
                p.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (p.Phone is not null && p.Phone.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        var roster = participantsQuery
            .OrderBy(p => p.FullName)
            .Select(p => new GuideGroupParticipantItemResponse
            {
                ParticipantId = p.ParticipantId,
                FullName = p.FullName,
                Age = CalculateAge(p.DateOfBirth, today),
                Phone = p.Phone,
                CurrentAttendanceStatus = currentActivityAttendances.GetValueOrDefault(p.ParticipantId)
            })
            .ToList();

        return new GuideGroupRosterResponse
        {
            GroupId = group.TripGroupId,
            GroupName = group.GroupName,
            TripId = group.TripId,
            TourName = group.Trip.Tour?.Name ?? string.Empty,
            CurrentActivity = currentActivity is null
                ? null
                : new GuideCurrentActivityResponse
                {
                    ActivityId = currentActivity.Id,
                    Name = currentActivity.Name,
                    DayNumber = currentActivity.DayNumber,
                    StartTime = currentActivity.StartTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                },
            Participants = roster
        };
    }

    public async Task<GuideParticipantDetailResponse> GetParticipantDetailAsync(
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var guideUserId = currentUser.RequiredUserId;
        var participant = await unitOfWork.BookingParticipants.GetParticipantForGuideAsync(
            participantId,
            cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hành khách");

        if (participant.TripGroup is null || participant.TripGroup.GuideUserId != guideUserId)
        {
            throw new ForbiddenException(
                "Bạn không có quyền truy cập hồ sơ hành khách của nhóm khác",
                "GUIDE_NOT_ASSIGNED_TO_GROUP");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var assignedAssets = (participant.Trip?.TripProvidedItems ?? [])
            .SelectMany(item => item.AssetAssignments
                .Where(a => a.Status == AssignmentStatuses.Issued || a.Status == AssignmentStatuses.Reserved)
                .Select(a => new GuideAssignedAssetItemResponse
                {
                    AssetId = a.EquipmentAssetId,
                    AssetCode = a.EquipmentAsset?.AssetCode ?? string.Empty,
                    ProductName = item.ProductVariant?.Product?.Name ?? item.TourProvidedItem?.ItemName ?? string.Empty,
                    AssignedAt = a.AssignedAt
                }))
            .OrderBy(a => a.AssetCode)
            .ToList();

        var attendanceHistory = participant.ActivityAttendances
            .OrderBy(a => a.TourActivity?.DayNumber ?? 0)
            .ThenBy(a => a.TourActivity?.StartTime ?? TimeSpan.Zero)
            .Select(a => new GuideParticipantAttendanceItemResponse
            {
                ActivityId = a.TourActivityId,
                ActivityName = a.TourActivity?.Name ?? string.Empty,
                DayNumber = a.TourActivity?.DayNumber ?? 1,
                Status = a.AttendanceStatus,
                CheckedAt = new DateTimeOffset(DateTime.SpecifyKind(a.CheckedAt, DateTimeKind.Utc))
            })
            .ToList();

        return new GuideParticipantDetailResponse
        {
            ParticipantId = participant.ParticipantId,
            FullName = participant.FullName,
            DateOfBirth = participant.DateOfBirth,
            Age = CalculateAge(participant.DateOfBirth, today),
            Phone = participant.Phone,
            Email = participant.Email ?? string.Empty,
            EmergencyContact = new GuideEmergencyContactResponse
            {
                Name = participant.EmergencyContactName,
                Phone = participant.EmergencyContactPhone
            },
            AssignedAssets = assignedAssets,
            AttendanceHistory = attendanceHistory
        };
    }

    public async Task<GuideBatchAttendanceResponse> SubmitBatchAttendanceAsync(
        Guid tripId,
        GuideBatchAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActivityId == Guid.Empty)
            throw new BadRequestException("Mã hoạt động là bắt buộc", "VALIDATION_ERROR");

        var group = await GetRequiredGuideGroupAsync(tripId, cancellationToken);
        var activity = group.Trip.Tour?.Activities.FirstOrDefault(a => a.Id == request.ActivityId && !a.IsDeleted)
            ?? throw new NotFoundException("Không tìm thấy hoạt động trong lịch trình chuyến đi");

        var groupParticipants = group.BookingParticipants
            .Where(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled)
            .ToDictionary(p => p.ParticipantId);

        var totalCount = groupParticipants.Count;

        // Idempotency check cho offline queue sync
        if (!string.IsNullOrWhiteSpace(request.ClientBatchId))
        {
            var alreadyProcessed = await unitOfWork.ActivityAttendances.ExistsAuditLogByBatchIdAsync(
                tripId,
                request.ClientBatchId,
                cancellationToken);

            if (alreadyProcessed)
            {
                var currentRecords = await unitOfWork.ActivityAttendances.GetAttendancesForUpdateAsync(
                    request.ActivityId,
                    groupParticipants.Keys.ToArray(),
                    cancellationToken);
                var existingPresentCount = currentRecords.Count(r =>
                    r.AttendanceStatus is AttendanceStatuses.Present or AttendanceStatuses.Late);

                return new GuideBatchAttendanceResponse
                {
                    ActivityId = request.ActivityId,
                    UpdatedCount = 0,
                    PresentCount = existingPresentCount,
                    TotalCount = totalCount,
                    ProgressText = $"{existingPresentCount}/{totalCount} có mặt"
                };
            }
        }

        var recordsToApply = new List<GuideAttendanceRecordItemRequest>();
        if (request.MarkAllPresent)
        {
            recordsToApply.AddRange(groupParticipants.Keys.Select(pid => new GuideAttendanceRecordItemRequest
            {
                ParticipantId = pid,
                Status = AttendanceStatuses.Present,
                Reason = "Điểm danh nhanh: Tất cả có mặt"
            }));
        }
        else
        {
            if (request.Attendances is null || request.Attendances.Count == 0)
                throw new BadRequestException("Danh sách điểm danh không được rỗng", "VALIDATION_ERROR");

            recordsToApply.AddRange(request.Attendances);
        }

        foreach (var item in recordsToApply)
        {
            if (!groupParticipants.ContainsKey(item.ParticipantId))
            {
                throw new ForbiddenException(
                    "Bạn không có quyền điểm danh cho hành khách không thuộc nhóm mình phụ trách",
                    "GUIDE_NOT_ASSIGNED_TO_GROUP");
            }
        }

        var existingAttendances = await unitOfWork.ActivityAttendances.GetAttendancesForUpdateAsync(
            request.ActivityId,
            groupParticipants.Keys.ToArray(),
            cancellationToken);
        var existingByParticipant = existingAttendances.ToDictionary(a => a.ParticipantId);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var guideUserId = currentUser.RequiredUserId;
        var updatedCount = 0;

        foreach (var item in recordsToApply)
        {
            var normalizedStatus = AdminTripAttendanceService.NormalizeAttendanceStatus(item.Status);
            var participant = groupParticipants[item.ParticipantId];

            if (existingByParticipant.TryGetValue(item.ParticipantId, out var existing))
            {
                if (!string.Equals(existing.AttendanceStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    if (!request.MarkAllPresent && string.IsNullOrWhiteSpace(item.Reason))
                    {
                        throw new BadRequestException(
                            "Bắt buộc nhập lý do khi thay đổi trạng thái điểm danh đã lưu",
                            "REASON_REQUIRED_FOR_UPDATE");
                    }

                    var oldStatus = existing.AttendanceStatus;
                    existing.AttendanceStatus = normalizedStatus;
                    existing.CheckedAt = utcNow;
                    existing.CheckedByUserId = guideUserId;

                    await unitOfWork.Trips.AddAuditLogAsync(new AuditLog
                    {
                        AuditLogId = Guid.NewGuid(),
                        ActorUserId = guideUserId,
                        Action = "UPDATE_ATTENDANCE",
                        EntityType = "ActivityAttendance",
                        EntityId = tripId,
                        Details = $"Guide sửa điểm danh hoạt động '{activity.Name}' của khách '{participant.FullName}' từ '{oldStatus}' sang '{normalizedStatus}'. Lý do: {item.Reason?.Trim() ?? "Tất cả có mặt"}",
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
                    ParticipantId = item.ParticipantId,
                    AttendanceStatus = normalizedStatus,
                    CheckedAt = utcNow,
                    CheckedByUserId = guideUserId,
                    CreatedAt = utcNow
                };
                await unitOfWork.ActivityAttendances.AddAsync(created, cancellationToken);
                existingByParticipant[item.ParticipantId] = created;
                updatedCount++;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ClientBatchId))
        {
            await unitOfWork.Trips.AddAuditLogAsync(new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                ActorUserId = guideUserId,
                Action = "GUIDE_BATCH_ATTENDANCE",
                EntityType = "ActivityAttendance",
                EntityId = tripId,
                Details = $"[BatchId:{request.ClientBatchId.Trim()}] Đồng bộ điểm danh nhóm '{group.GroupName}' cho hoạt động '{activity.Name}' ({updatedCount} bản ghi).",
                CreatedAt = utcNow
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var presentCount = existingByParticipant.Values.Count(a =>
            a.AttendanceStatus is AttendanceStatuses.Present or AttendanceStatuses.Late);

        return new GuideBatchAttendanceResponse
        {
            ActivityId = request.ActivityId,
            UpdatedCount = updatedCount,
            PresentCount = presentCount,
            TotalCount = totalCount,
            ProgressText = $"{presentCount}/{totalCount} có mặt"
        };
    }

    public async Task<GuideTripItineraryResponse> GetTripItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var group = await GetRequiredGuideGroupAsync(tripId, cancellationToken);
        var groupParticipantIds = group.BookingParticipants
            .Where(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled)
            .Select(p => p.ParticipantId)
            .ToHashSet();
        var totalGroupCount = groupParticipantIds.Count;

        var activities = (group.Trip.Tour?.Activities ?? [])
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.DayNumber)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.StartTime)
            .ToList();

        var currentActivity = DetermineCurrentActivity(group.Trip, activities, timeProvider.GetUtcNow());
        var attendances = await unitOfWork.ActivityAttendances.GetAttendancesByTripAsync(tripId, cancellationToken);

        var attendedByActivity = attendances
            .Where(a => groupParticipantIds.Contains(a.ParticipantId) && a.AttendanceStatus is AttendanceStatuses.Present or AttendanceStatuses.Late)
            .GroupBy(a => a.TourActivityId)
            .ToDictionary(g => g.Key, g => g.Count());

        var days = activities
            .GroupBy(a => a.DayNumber)
            .OrderBy(g => g.Key)
            .Select(dayGroup =>
            {
                var firstWithTitle = dayGroup.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.DayTitle));
                var title = !string.IsNullOrWhiteSpace(firstWithTitle?.DayTitle)
                    ? $"Ngày {dayGroup.Key}: {firstWithTitle.DayTitle}"
                    : $"Ngày {dayGroup.Key}";

                return new GuideItineraryDayItemResponse
                {
                    DayNumber = dayGroup.Key,
                    Title = title,
                    Activities = dayGroup.Select(a =>
                    {
                        var presentCount = attendedByActivity.GetValueOrDefault(a.Id, 0);
                        return new GuideItineraryActivityItemResponse
                        {
                            ActivityId = a.Id,
                            Name = a.Name,
                            StartTime = a.StartTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
                            DurationMinutes = a.DurationMinutes,
                            Description = a.Description,
                            IsCurrent = currentActivity?.Id == a.Id,
                            AttendanceRate = $"{presentCount}/{totalGroupCount}"
                        };
                    }).ToList()
                };
            })
            .ToList();

        return new GuideTripItineraryResponse
        {
            TripId = group.TripId,
            TourName = group.Trip.Tour?.Name ?? string.Empty,
            Days = days
        };
    }

    public async Task<GuideTripProvidedItemsResponse> GetProvidedItemsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        _ = await GetRequiredGuideGroupAsync(tripId, cancellationToken);

        var items = await unitOfWork.Trips.GetTripProvidedItemsWithAssetsAsync(tripId, cancellationToken);

        var mappedItems = items.Select(item =>
        {
            var allocatedAssets = item.AssetAssignments
                .Where(a => a.Status != AssignmentStatuses.Cancelled)
                .OrderBy(a => a.EquipmentAsset?.AssetCode)
                .Select(a => new GuideAllocatedAssetItemResponse
                {
                    AssetCode = a.EquipmentAsset?.AssetCode ?? string.Empty,
                    Status = a.Status
                })
                .ToList();

            return new GuideProvidedItemResponse
            {
                ProvidedItemId = item.TripProvidedItemId,
                ProductName = item.ProductVariant?.Product?.Name ?? item.TourProvidedItem?.ItemName ?? string.Empty,
                VariantName = item.ProductVariant?.Name ?? "Tiêu chuẩn",
                RequiredQuantity = item.RequiredQuantity,
                AllocatedQuantity = allocatedAssets.Count,
                Status = item.Status,
                AllocatedAssets = allocatedAssets
            };
        }).ToList();

        return new GuideTripProvidedItemsResponse
        {
            TripId = tripId,
            Items = mappedItems
        };
    }

    private async Task<TripGroup> GetRequiredGuideGroupAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        _ = await unitOfWork.TripGroups.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var guideUserId = currentUser.RequiredUserId;
        var group = await unitOfWork.TripGroups.GetGuideGroupForTripAsync(tripId, guideUserId, cancellationToken);
        if (group is null)
        {
            throw new ForbiddenException(
                "Bạn không có quyền điểm danh cho nhóm hoặc chuyến đi này",
                "GUIDE_NOT_ASSIGNED_TO_GROUP");
        }

        return group;
    }

    private static TourActivity? DetermineCurrentActivity(
        Trip trip,
        IReadOnlyList<TourActivity> activities,
        DateTimeOffset utcNow)
    {
        if (activities.Count == 0)
            return null;

        var startDate = DateOnly.FromDateTime(trip.StartAt.UtcDateTime);
        var currentDate = DateOnly.FromDateTime(utcNow.UtcDateTime);
        var currentDayNumber = Math.Clamp((currentDate.DayNumber - startDate.DayNumber) + 1, 1, Math.Max(1, activities.Max(a => a.DayNumber)));

        var dayActivities = activities.Where(a => a.DayNumber == currentDayNumber).ToList();
        if (dayActivities.Count == 0)
            return activities[0];

        var nowTime = utcNow.UtcDateTime.TimeOfDay;
        var ongoing = dayActivities
            .LastOrDefault(a => a.StartTime <= nowTime);

        return ongoing ?? dayActivities[0];
    }

    private static int? CalculateAge(DateOnly? dateOfBirth, DateOnly today)
    {
        if (!dateOfBirth.HasValue)
            return null;

        var age = today.Year - dateOfBirth.Value.Year;
        if (dateOfBirth.Value > today.AddYears(-age))
            age--;

        return Math.Max(0, age);
    }
}
