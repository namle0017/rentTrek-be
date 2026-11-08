using Microsoft.Extensions.Logging;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using static RenTrek.BLL.Helpers.TripGroupRules;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Cấu hình chia nhóm của Tour, lịch lặp và sinh Trip tự động từ lịch lặp.</summary>
public sealed class TripRecurrenceService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    ILogger<TripRecurrenceService> logger) : ITripRecurrenceService
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
                unitOfWork,
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
                unitOfWork,
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
        var overflowSlots = await GetGroupOverflowSlotsAsync(unitOfWork, cancellationToken);
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
                        unitOfWork,
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
                        unitOfWork,
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

    private static List<DateOnly> EnumerateOccurrences(
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

    private static List<Guid> ResolveWarningRecipients(
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
}
