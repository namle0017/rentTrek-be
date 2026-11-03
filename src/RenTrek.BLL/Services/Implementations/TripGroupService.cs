using System.Text.Json;
using Microsoft.Extensions.Logging;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện toàn bộ nghiệp vụ chia TripGroup (mục 7): cấu hình Tour &amp; lịch lặp, tạo/mở Trip, tự xếp sau thanh toán,
/// điều phối Staff (guide, chuyển, tách, tính lại đồ cấp, chốt Published), màn hình Guide và màn hình Khách.
/// </summary>
public sealed class TripGroupService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    ILogger<TripGroupService> logger) : ITripGroupService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<TourGroupSettingsResponse> UpdateTourGroupSettingsAsync(
        Guid tourId,
        UpdateTourGroupSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.GroupMaxParticipants <= 0)
            throw new BadRequestException("Sức chứa chuẩn của nhóm (GroupMaxParticipants) phải lớn hơn 0", ErrorCodes.ValidationError);

        var actorUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var tour = await unitOfWork.TripGroups.GetTourForWriteAsync(tourId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tour", ErrorCodes.ItemNotFound);

            var previous = tour.GroupMaxParticipants;
            tour.GroupMaxParticipants = request.GroupMaxParticipants;
            tour.UpdatedAt = utcNow.UtcDateTime;

            await AddAuditLogAsync(
                actorUserId,
                "UpdateTourGroupSettings",
                "Tour",
                tour.Id,
                new { previousGroupMaxParticipants = previous, groupMaxParticipants = tour.GroupMaxParticipants },
                utcNow,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return new TourGroupSettingsResponse
            {
                TourId = tour.Id,
                TourName = tour.Name,
                Version = tour.Version,
                GroupMaxParticipants = tour.GroupMaxParticipants.Value
            };
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<TourRecurrenceRuleResponse> CreateRecurrenceRuleAsync(
        Guid tourId,
        CreateTourRecurrenceRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var frequency = NormalizeFrequency(request.Frequency);
        if (request.IntervalValue < 1)
            throw new BadRequestException("IntervalValue phải lớn hơn hoặc bằng 1", ErrorCodes.ValidationError);
        if (request.EndDate < request.StartDate)
            throw new BadRequestException("EndDate không được nhỏ hơn StartDate", ErrorCodes.ValidationError);
        if (request.DefaultTotalSlots <= 0)
            throw new BadRequestException("DefaultTotalSlots phải lớn hơn 0", ErrorCodes.ValidationError);
        if (request.DefaultGroupCount <= 0)
            throw new BadRequestException("Quy tắc lịch lặp mới bắt buộc có DefaultGroupCount lớn hơn 0", ErrorCodes.ValidationError);
        if (request.DefaultPrice < 0)
            throw new BadRequestException("DefaultPrice không được âm", ErrorCodes.ValidationError);
        if (!string.IsNullOrWhiteSpace(request.DaysOfWeek))
            _ = ParseDaysOfWeek(request.DaysOfWeek);

        var actorUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var tour = await unitOfWork.TripGroups.GetTourForWriteAsync(tourId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tour", ErrorCodes.ItemNotFound);

            if (tour.GroupMaxParticipants is null or <= 0)
            {
                throw new ConflictException(
                    "Tour chưa cấu hình GroupMaxParticipants hợp lệ, không thể tạo lịch lặp sinh group",
                    ErrorCodes.TripOperationalCapacityInsufficient);
            }

            var rule = new TourRecurrenceRule
            {
                RecurrenceRuleId = Guid.NewGuid(),
                TourId = tour.Id,
                Tour = tour,
                Frequency = frequency,
                IntervalValue = request.IntervalValue,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                StartTime = request.StartTime,
                DaysOfWeek = string.IsNullOrWhiteSpace(request.DaysOfWeek) ? null : request.DaysOfWeek.Trim(),
                DefaultTotalSlots = request.DefaultTotalSlots,
                DefaultGroupCount = request.DefaultGroupCount,
                DefaultPrice = request.DefaultPrice,
                IsActive = request.IsActive,
                CreatedAt = utcNow.UtcDateTime,
                UpdatedAt = utcNow.UtcDateTime
            };

            await unitOfWork.TourRecurrenceRules.AddAsync(rule, cancellationToken);
            await AddAuditLogAsync(
                actorUserId,
                "CreateTourRecurrenceRule",
                "TourRecurrenceRule",
                rule.RecurrenceRuleId,
                new
                {
                    tourId = tour.Id,
                    rule.Frequency,
                    rule.IntervalValue,
                    rule.DefaultTotalSlots,
                    rule.DefaultGroupCount
                },
                utcNow,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapRecurrenceRule(rule);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<TourRecurrenceRuleResponse>> GetRecurrenceRulesAsync(
        Guid tourId,
        CancellationToken cancellationToken = default)
    {
        var rules = await unitOfWork.TripGroups.GetRecurrenceRulesByTourAsync(tourId, cancellationToken);
        return rules.Select(MapRecurrenceRule).ToList();
    }

    public async Task<GenerateRecurringTripsResponse> GenerateRecurringTripsAsync(
        GenerateRecurringTripsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FromDate.HasValue && request.ToDate.HasValue && request.ToDate.Value < request.FromDate.Value)
            throw new BadRequestException("ToDate không được nhỏ hơn FromDate", ErrorCodes.ValidationError);

        var actorUserId = currentUser.UserId;
        var utcNow = timeProvider.GetUtcNow();
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);
        var rules = await unitOfWork.TripGroups.GetActiveRecurrenceRulesAsync(request.TourId, cancellationToken);

        var generatedTrips = new List<AdminTripResponse>();
        var warnings = new List<RecurrenceGenerationWarningResponse>();
        var generatedGroupsCount = 0;
        var skippedExistingCount = 0;

        if (rules.Count == 0)
            return new GenerateRecurringTripsResponse();

        var refundPolicyId = await unitOfWork.TripGroups.GetActiveTourRefundPolicyIdAsync(cancellationToken);
        var managerUserId = actorUserId ?? await unitOfWork.TripGroups.GetDefaultManagerUserIdAsync(cancellationToken);
        var adminIds = await unitOfWork.TripGroups.GetAdminAccountIdsAsync(cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var rule in rules)
            {
                await unitOfWork.TripGroups.AcquireRecurrenceRuleLockAsync(rule.RecurrenceRuleId, cancellationToken);

                if (rule.DefaultGroupCount is null or <= 0 || rule.Tour?.GroupMaxParticipants is null or <= 0)
                {
                    var code = rule.DefaultGroupCount is null or <= 0
                        ? "RECURRENCE_MISSING_DEFAULT_GROUP_COUNT"
                        : "TOUR_MISSING_GROUP_MAX_PARTICIPANTS";
                    var message = rule.DefaultGroupCount is null or <= 0
                        ? $"Rule lịch lặp {rule.RecurrenceRuleId} chưa cấu hình DefaultGroupCount nên bị chặn sinh Trip mới."
                        : $"Tour {rule.TourId} chưa cấu hình GroupMaxParticipants nên rule {rule.RecurrenceRuleId} bị chặn sinh Trip mới.";

                    logger.LogWarning(
                        "Cảnh báo vận hành khi sinh Trip từ lịch lặp {RuleId} (Tour {TourId}): {Message}",
                        rule.RecurrenceRuleId,
                        rule.TourId,
                        message);

                    warnings.Add(new RecurrenceGenerationWarningResponse
                    {
                        RecurrenceRuleId = rule.RecurrenceRuleId,
                        TourId = rule.TourId,
                        Code = code,
                        Message = message
                    });

                    await AddAuditLogAsync(
                        actorUserId,
                        "RecurrenceRuleMissingGroupConfig",
                        "TourRecurrenceRule",
                        rule.RecurrenceRuleId,
                        new { rule.TourId, code, message },
                        utcNow,
                        cancellationToken);

                    foreach (var recipientId in ResolveWarningRecipients(actorUserId, rule.Tour?.CreatedByUserId, adminIds))
                    {
                        await unitOfWork.Notifications.AddAsync(new Notification
                        {
                            NotificationId = Guid.NewGuid(),
                            RecipientUserId = recipientId,
                            NotificationType = "OperationalWarning",
                            Title = "Lịch lặp thiếu cấu hình chia nhóm",
                            Content = message,
                            CreatedAt = utcNow.UtcDateTime,
                            RelatedEntityType = "TourRecurrenceRule",
                            RelatedEntityId = rule.RecurrenceRuleId
                        }, cancellationToken);
                    }

                    continue;
                }

                if (refundPolicyId is null)
                    throw new ConflictException("Hệ thống chưa có chính sách hoàn tiền TOUR đang hoạt động để sinh Trip");
                if (managerUserId is null)
                    throw new ConflictException("Không xác định được tài khoản quản lý Trip");

                var occurrences = EnumerateOccurrences(rule, request.FromDate, request.ToDate);
                var existingStarts = rule.Trips
                    .Where(t => !t.IsDeleted)
                    .Select(t => t.StartAt.ToUniversalTime())
                    .ToHashSet();

                foreach (var occurrenceDate in occurrences)
                {
                    var startAt = new DateTimeOffset(occurrenceDate.ToDateTime(rule.StartTime), VietnamOffset).ToUniversalTime();
                    if (existingStarts.Contains(startAt))
                    {
                        skippedExistingCount++;
                        continue;
                    }

                    var endAt = CalculateTripEndAt(startAt, rule.Tour);
                    var trip = BuildDraftTripWithGroups(
                        rule.Tour,
                        rule.RecurrenceRuleId,
                        refundPolicyId.Value,
                        managerUserId.Value,
                        startAt,
                        endAt,
                        rule.DefaultTotalSlots,
                        rule.DefaultGroupCount.Value,
                        rule.DefaultPrice,
                        minParticipants: 1,
                        utcNow);

                    rule.Trips.Add(trip);
                    existingStarts.Add(startAt);
                    await unitOfWork.Trips.AddAsync(trip, cancellationToken);
                    if (trip.TripGroups.Count > 0)
                        await unitOfWork.TripGroups.AddRangeAsync(trip.TripGroups, cancellationToken);

                    generatedGroupsCount += trip.TripGroups.Count;
                    generatedTrips.Add(MapAdminTrip(trip, overflowSlots));

                    await AddAuditLogAsync(
                        actorUserId,
                        "GenerateTripFromRecurrenceRule",
                        "Trip",
                        trip.TripId,
                        new
                        {
                            recurrenceRuleId = rule.RecurrenceRuleId,
                            tourId = rule.TourId,
                            trip.StartAt,
                            trip.TotalSlots,
                            groupCount = trip.TripGroups.Count
                        },
                        utcNow,
                        cancellationToken);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return new GenerateRecurringTripsResponse
            {
                GeneratedTripsCount = generatedTrips.Count,
                GeneratedGroupsCount = generatedGroupsCount,
                SkippedExistingCount = skippedExistingCount,
                GeneratedTrips = generatedTrips,
                Warnings = warnings
            };
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AdminTripResponse> GetAdminTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: false, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);
        return MapAdminTrip(trip, overflowSlots);
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
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);

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
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);

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
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);

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

            trip.Status = targetStatus;
            trip.UpdatedAt = utcNow;
            trip.RowVersion++;

            await AddAuditLogAsync(
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
            .Where(p => !p.IsDeleted && !string.Equals(p.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
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

        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);
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
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);

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

            EnsureExpectedRowVersion(trip, request.ExpectedTripRowVersion);

            var group = GetActiveGroups(trip).SingleOrDefault(g => g.TripGroupId == groupId)
                ?? throw new BadRequestException("Group không thuộc chuyến đi này", ErrorCodes.GroupWrongTrip);

            if (string.Equals(group.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "Group đã chốt (Published), không được đổi hướng dẫn viên qua API thường",
                    ErrorCodes.TripGroupingLocked);
            }

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
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);

            EnsureExpectedRowVersion(trip, request.ExpectedTripRowVersion);

            var activeGroups = GetActiveGroups(trip).ToDictionary(g => g.TripGroupId);
            if (!activeGroups.TryGetValue(request.TargetGroupId, out var targetGroup))
                throw new BadRequestException("Group đích không thuộc cùng Trip", ErrorCodes.GroupWrongTrip);

            if (string.Equals(targetGroup.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "Group đích đã chốt (Published), không được chuyển booking bằng API thường",
                    ErrorCodes.TripGroupingLocked);
            }

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
                                         string.Equals(src.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase)))
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
        if (approver is null || !string.Equals(approver.Status, "Active", StringComparison.OrdinalIgnoreCase))
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
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);

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
                                          string.Equals(src.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException(
                    "Booking đang có người thuộc group đã chốt (Published), không được tách bằng API thường",
                    ErrorCodes.TripGroupingLocked);
            }

            foreach (var groupCountPair in flatAssignments.GroupBy(a => a.TargetGroupId))
            {
                var targetGroup = activeGroups[groupCountPair.Key];
                if (string.Equals(targetGroup.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConflictException(
                        "Group đích đã chốt (Published), không được tách booking vào group này bằng API thường",
                        ErrorCodes.TripGroupingLocked);
                }

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
                    existing.Status = "Ready";
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
                        Status = "Ready",
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
        var overflowSlots = await GetGroupOverflowSlotsAsync(cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
            var trip = await unitOfWork.TripGroups.GetTripWorkspaceGraphAsync(tripId, asTracking: true, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", ErrorCodes.ItemNotFound);

            EnsureExpectedRowVersion(trip, request?.ExpectedTripRowVersion);

            var activeGroups = GetActiveGroups(trip).ToList();
            if (activeGroups.Count > 0 &&
                activeGroups.All(g => string.Equals(g.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase)))
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
                !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
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
                        !string.Equals(p.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                        (isBuyer || p.LinkedUserId == userId))
            .Where(p => p.TripGroup is not null &&
                        !p.TripGroup.IsDeleted &&
                        string.Equals(p.TripGroup.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase))
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

    internal static bool IsActivePaidOrder(Order? order)
        => order is not null
           && !order.IsDeleted
           && string.Equals(order.Status, OrderStatuses.Confirmed, StringComparison.OrdinalIgnoreCase)
           && string.Equals(order.PaymentStatus, OrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase);

    internal static bool IsOperationalParticipant(BookingParticipant participant)
        => !participant.IsDeleted
           && !string.Equals(participant.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
           && IsActivePaidOrder(participant.BookingOrder?.Order);

    private async Task<Guid?> ResolveGuideScopeUserIdAsync(CancellationToken cancellationToken)
    {
        var role = currentUser.Role;
        if (string.Equals(role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, RoleNames.Staff, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var userId = currentUser.RequiredUserId;
        if (string.Equals(role, RoleNames.Guide, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, RoleNames.TourGuide, StringComparison.OrdinalIgnoreCase))
        {
            return userId;
        }

        var account = await unitOfWork.TripGroups.GetAccountWithRoleAsync(userId, cancellationToken);
        var accountRole = account?.Role?.RoleName;
        if (string.Equals(accountRole, RoleNames.Guide, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(accountRole, RoleNames.TourGuide, StringComparison.OrdinalIgnoreCase))
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

    private static void EnsureExpectedRowVersion(Trip trip, int? expectedTripRowVersion)
    {
        if (expectedTripRowVersion.HasValue && trip.RowVersion != expectedTripRowVersion.Value)
        {
            throw new ConflictException(
                "Dữ liệu chuyến đi đã được cập nhật bởi thao tác khác, vui lòng tải lại",
                ErrorCodes.ConcurrencyConflict);
        }
    }

    private async Task<int> GetGroupOverflowSlotsAsync(CancellationToken cancellationToken)
    {
        var raw = await unitOfWork.SystemConfigurations.GetValueAsync(TripGroupConfigKeys.GroupOverflowSlots, cancellationToken);
        return int.TryParse(raw, out var parsed) && parsed >= 0
            ? parsed
            : TripGroupConfigKeys.DefaultGroupOverflowSlots;
    }

    private static Trip BuildDraftTripWithGroups(
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

    private static IReadOnlyList<PublishBlockerResponse> GetPublishBlockers(Trip trip, DateTimeOffset utcNow)
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

    private static bool AreProvidedItemsUpToDate(Trip trip)
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

    private static int ComputeRequiredItemQuantity(
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

    private static IEnumerable<TripGroup> GetActiveGroups(Trip trip)
        => trip.TripGroups
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.TripGroupId);

    private static List<BookingParticipant> GetTripOperationalParticipants(Trip trip)
    {
        var byId = new Dictionary<Guid, BookingParticipant>();

        foreach (var booking in trip.TripBookingOrders)
        {
            if (!IsActivePaidOrder(booking.Order))
                continue;

            foreach (var participant in booking.Participants)
            {
                if (!participant.IsDeleted && !string.Equals(participant.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
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

    private static List<BookingParticipant> GetGroupOperationalParticipants(
        Trip trip,
        Guid groupId,
        Guid? excludeOrderId = null)
        => GetTripOperationalParticipants(trip)
            .Where(p => p.TripGroupId == groupId && (!excludeOrderId.HasValue || p.BookingOrderId != excludeOrderId.Value))
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.ParticipantId)
            .ToList();

    private static int CountOperationalParticipants(
        Trip trip,
        Guid groupId,
        Guid? excludeOrderId = null)
        => GetGroupOperationalParticipants(trip, groupId, excludeOrderId).Count;

    private static bool IsValidActiveGuideAccount(Account account)
        => !account.IsDeleted
           && string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase)
           && account.Role is not null
           && (string.Equals(account.Role.RoleName, RoleNames.Guide, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(account.Role.RoleName, RoleNames.TourGuide, StringComparison.OrdinalIgnoreCase));

    private static AdminTripResponse MapAdminTrip(Trip trip, int overflowSlots)
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

    private static TripGroupingSnapshotResponse MapGroupingSnapshot(
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
                             allActiveGroups.All(g => string.Equals(g.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase))
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
                                !string.Equals(p.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
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
                         allActiveGroups.Any(g => !string.Equals(g.Status, TripGroupStatuses.Published, StringComparison.OrdinalIgnoreCase)),
            PublishBlockers = blockers,
            Groups = groupCards,
            UnassignedBookings = unassignedBookings,
            ProvidedItemsNeedsRecalc = trip.ProvidedItemsNeedsRecalc || !AreProvidedItemsUpToDate(trip),
            ProvidedItems = MapProvidedItems(trip)
        };
    }

    private static IReadOnlyList<TripProvidedItemSummaryResponse> MapProvidedItems(Trip trip)
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

    private static IReadOnlyList<TripActivityPreviewResponse> BuildTripActivityPreviews(Trip trip, DateTimeOffset utcNow)
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

    private static TourRecurrenceRuleResponse MapRecurrenceRule(TourRecurrenceRule rule)
        => new()
        {
            RecurrenceRuleId = rule.RecurrenceRuleId,
            TourId = rule.TourId,
            Frequency = rule.Frequency,
            IntervalValue = rule.IntervalValue,
            StartDate = rule.StartDate,
            EndDate = rule.EndDate,
            StartTime = rule.StartTime,
            DaysOfWeek = rule.DaysOfWeek,
            DefaultTotalSlots = rule.DefaultTotalSlots,
            DefaultGroupCount = rule.DefaultGroupCount,
            DefaultPrice = rule.DefaultPrice,
            IsActive = rule.IsActive
        };

    private static DateTimeOffset CalculateTripEndAt(DateTimeOffset startAt, Tour? tour)
    {
        if (tour is not null && tour.DurationMinutes > 0)
            return startAt.AddMinutes(tour.DurationMinutes);
        var days = tour is not null && tour.DurationDays > 0 ? tour.DurationDays : 1;
        return startAt.AddDays(days);
    }

    private static IReadOnlyList<DateOnly> EnumerateOccurrences(
        TourRecurrenceRule rule,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var windowStart = fromDate.HasValue && fromDate.Value > rule.StartDate ? fromDate.Value : rule.StartDate;
        var windowEnd = toDate.HasValue && toDate.Value < rule.EndDate ? toDate.Value : rule.EndDate;
        if (windowEnd < windowStart)
            return [];

        var interval = Math.Max(1, rule.IntervalValue);
        var frequency = NormalizeFrequency(rule.Frequency);
        var results = new List<DateOnly>();

        if (frequency == "Daily")
        {
            for (var d = rule.StartDate; d <= windowEnd; d = d.AddDays(interval))
            {
                if (d >= windowStart)
                    results.Add(d);
            }
        }
        else if (frequency == "Weekly")
        {
            var days = string.IsNullOrWhiteSpace(rule.DaysOfWeek)
                ? [rule.StartDate.DayOfWeek]
                : ParseDaysOfWeek(rule.DaysOfWeek);

            var anchorMonday = StartOfIsoWeek(rule.StartDate);
            for (var d = rule.StartDate; d <= windowEnd; d = d.AddDays(1))
            {
                var currentMonday = StartOfIsoWeek(d);
                var weekDiff = (currentMonday.DayNumber - anchorMonday.DayNumber) / 7;
                if (weekDiff % interval == 0 && days.Contains(d.DayOfWeek) && d >= windowStart)
                    results.Add(d);
            }
        }
        else if (frequency == "Monthly")
        {
            for (var step = 0; ; step++)
            {
                var d = rule.StartDate.AddMonths(step * interval);
                if (d > windowEnd)
                    break;
                if (d >= windowStart)
                    results.Add(d);
            }
        }

        return results;
    }

    private static DateOnly StartOfIsoWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static HashSet<DayOfWeek> ParseDaysOfWeek(string raw)
    {
        var set = new HashSet<DayOfWeek>();
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var day = token.ToLowerInvariant() switch
            {
                "mon" or "monday" or "1" or "t2" => DayOfWeek.Monday,
                "tue" or "tuesday" or "2" or "t3" => DayOfWeek.Tuesday,
                "wed" or "wednesday" or "3" or "t4" => DayOfWeek.Wednesday,
                "thu" or "thursday" or "4" or "t5" => DayOfWeek.Thursday,
                "fri" or "friday" or "5" or "t6" => DayOfWeek.Friday,
                "sat" or "saturday" or "6" or "t7" => DayOfWeek.Saturday,
                "sun" or "sunday" or "0" or "7" or "cn" => DayOfWeek.Sunday,
                _ => throw new BadRequestException($"Giá trị DaysOfWeek '{token}' không hợp lệ", ErrorCodes.ValidationError)
            };
            set.Add(day);
        }

        if (set.Count == 0)
            throw new BadRequestException("DaysOfWeek không hợp lệ", ErrorCodes.ValidationError);
        return set;
    }

    private static string NormalizeFrequency(string? frequency)
        => (frequency ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "daily" => "Daily",
            "weekly" => "Weekly",
            "monthly" => "Monthly",
            _ => throw new BadRequestException("Frequency chỉ hỗ trợ Daily, Weekly hoặc Monthly", ErrorCodes.ValidationError)
        };

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

    private static IReadOnlyList<Guid> ResolveWarningRecipients(
        Guid? actorUserId,
        Guid? tourCreatorId,
        IReadOnlyList<Guid> adminIds)
    {
        var recipients = new HashSet<Guid>(adminIds);
        if (actorUserId is { } aid && aid != Guid.Empty)
            recipients.Add(aid);
        if (tourCreatorId is { } cid && cid != Guid.Empty)
            recipients.Add(cid);
        return recipients.ToList();
    }

    private Task AddAuditLogAsync(
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
}
