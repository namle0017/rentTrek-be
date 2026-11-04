using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ quản lý quy tắc lặp chuyến đi và sinh chuyến hàng loạt (Màn O08).
/// </summary>
public sealed class AdminRecurrenceRuleService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IAdminRecurrenceRuleService
{
    public async Task<IReadOnlyList<RecurrenceRuleResponse>> GetRulesByTourAsync(
        Guid tourId,
        CancellationToken cancellationToken = default)
    {
        var tour = await unitOfWork.TourRecurrenceRules.GetTourByIdAsync(tourId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tour");

        var rules = await unitOfWork.TourRecurrenceRules.GetByTourIdAsync(tour.Id, cancellationToken);
        return rules.Select(MapRuleResponse).ToList();
    }

    public async Task<RecurrenceRuleResponse> CreateRuleAsync(
        Guid tourId,
        CreateRecurrenceRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRuleInput(
            request.Frequency,
            request.IntervalValue,
            request.StartTime,
            request.StartDate,
            request.EndDate,
            request.DefaultTotalSlots,
            request.DefaultPrice,
            out var parsedStartTime,
            out var normalizedFrequency);

        var tour = await unitOfWork.TourRecurrenceRules.GetTourByIdAsync(tourId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tour");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rule = new TourRecurrenceRule
        {
            RecurrenceRuleId = Guid.NewGuid(),
            TourId = tour.Id,
            Frequency = normalizedFrequency,
            IntervalValue = request.IntervalValue,
            DaysOfWeek = string.IsNullOrWhiteSpace(request.DaysOfWeek) ? null : request.DaysOfWeek.Trim(),
            StartTime = parsedStartTime,
            StartDate = request.StartDate,
            EndDate = request.EndDate ?? request.StartDate.AddMonths(3),
            DefaultTotalSlots = request.DefaultTotalSlots,
            DefaultPrice = request.DefaultPrice ?? tour.BasePrice,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        await unitOfWork.TourRecurrenceRules.AddAsync(rule, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapRuleResponse(rule);
    }

    public async Task<RecurrenceRuleResponse> UpdateRuleAsync(
        Guid ruleId,
        UpdateRecurrenceRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRuleInput(
            request.Frequency,
            request.IntervalValue,
            request.StartTime,
            request.StartDate,
            request.EndDate,
            request.DefaultTotalSlots,
            request.DefaultPrice,
            out var parsedStartTime,
            out var normalizedFrequency);

        var rule = await unitOfWork.TourRecurrenceRules.GetRuleForUpdateAsync(ruleId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy quy tắc lặp chuyến");

        rule.Frequency = normalizedFrequency;
        rule.IntervalValue = request.IntervalValue;
        rule.DaysOfWeek = string.IsNullOrWhiteSpace(request.DaysOfWeek) ? null : request.DaysOfWeek.Trim();
        rule.StartTime = parsedStartTime;
        rule.StartDate = request.StartDate;
        rule.EndDate = request.EndDate ?? request.StartDate.AddMonths(3);
        rule.DefaultTotalSlots = request.DefaultTotalSlots;
        rule.DefaultPrice = request.DefaultPrice ?? rule.Tour.BasePrice;
        rule.IsActive = request.IsActive;
        rule.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MapRuleResponse(rule);
    }

    public async Task DeleteRuleAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        var rule = await unitOfWork.TourRecurrenceRules.GetRuleForUpdateAsync(ruleId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy quy tắc lặp chuyến");

        rule.IsDeleted = true;
        rule.IsActive = false;
        rule.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurrencePreviewItemResponse>> PreviewTripsAsync(
        Guid ruleId,
        PreviewRecurrenceTripsRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var rule = await unitOfWork.TourRecurrenceRules.GetRuleWithTourAsync(ruleId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy quy tắc lặp chuyến");

        return await BuildPreviewItemsAsync(rule, request?.FromDate, request?.ToDate, cancellationToken);
    }

    public async Task<GenerateRecurrenceTripsResponse> GenerateTripsAsync(
        Guid ruleId,
        GenerateRecurrenceTripsRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var rule = await unitOfWork.TourRecurrenceRules.GetRuleWithTourAsync(ruleId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy quy tắc lặp chuyến");

        if (!rule.IsActive)
            throw new ConflictException("Quy tắc lặp chuyến đang bị vô hiệu hóa", "RULE_INACTIVE");

        if (!IsTourPublishable(rule.Tour.Status))
            throw new ConflictException("Tour chưa được xuất bản hoặc đã ngừng hoạt động", "TOUR_NOT_PUBLISHED");

        var refundPolicy = await unitOfWork.TourRecurrenceRules.GetDefaultOrSpecificRefundPolicyAsync(
            request?.RefundPolicyId,
            cancellationToken)
            ?? throw new ConflictException("Không tìm thấy chính sách hoàn tiền đang hoạt động", "REFUND_POLICY_INACTIVE");

        var previews = await BuildPreviewItemsAsync(rule, request?.FromDate, request?.ToDate, cancellationToken);
        var toCreate = previews.Where(x => !x.IsAlreadyGenerated).ToList();
        var utcNow = timeProvider.GetUtcNow();
        var managedByUserId = request?.ManagedByUserId
                              ?? currentUser.UserId
                              ?? rule.Tour.CreatedByUserId
                              ?? refundPolicy.CreatedByUserId;
        var minParticipants = request?.MinParticipants.HasValue == true && request.MinParticipants.Value > 0
            ? Math.Min(request.MinParticipants.Value, rule.DefaultTotalSlots)
            : Math.Max(1, Math.Min(10, rule.DefaultTotalSlots / 2));

        var createdIds = new List<Guid>(toCreate.Count);
        var newTrips = new List<Trip>(toCreate.Count);
        var providedItemsToCreate = new List<TripProvidedItem>();

        foreach (var item in toCreate)
        {
            var tripId = Guid.NewGuid();
            createdIds.Add(tripId);

            newTrips.Add(new Trip
            {
                TripId = tripId,
                TourId = rule.TourId,
                RecurrenceRuleId = rule.RecurrenceRuleId,
                RefundPolicyId = refundPolicy.RefundPolicyId,
                ManagedByUserId = managedByUserId,
                StartAt = item.StartAt,
                EndAt = item.EndAt,
                TotalSlots = rule.DefaultTotalSlots,
                MinParticipants = minParticipants,
                PricePerPerson = rule.DefaultPrice > 0 ? rule.DefaultPrice : rule.Tour.BasePrice,
                Status = "Draft",
                RowVersion = 1,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

            foreach (var tourProvided in rule.Tour.ProvidedItems.Where(p => !p.IsDeleted && p.ProductVariantId.HasValue))
            {
                var reqQty = string.Equals(tourProvided.QuantityBasis, "PerPerson", StringComparison.OrdinalIgnoreCase)
                    ? tourProvided.Quantity * rule.DefaultTotalSlots
                    : tourProvided.Quantity;

                providedItemsToCreate.Add(new TripProvidedItem
                {
                    TripProvidedItemId = Guid.NewGuid(),
                    TripId = tripId,
                    TourProvidedItemId = tourProvided.Id,
                    ProductVariantId = tourProvided.ProductVariantId!.Value,
                    RequiredQuantity = Math.Max(1, reqQty),
                    Status = "Missing",
                    CreatedAt = utcNow.UtcDateTime
                });
            }
        }

        if (newTrips.Count > 0)
        {
            await unitOfWork.Trips.AddRangeAsync(newTrips, cancellationToken);
            if (providedItemsToCreate.Count > 0)
                await unitOfWork.Trips.AddTripProvidedItemsAsync(providedItemsToCreate, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new GenerateRecurrenceTripsResponse
        {
            RuleId = rule.RecurrenceRuleId,
            CreatedCount = newTrips.Count,
            SkippedCount = previews.Count - newTrips.Count,
            CreatedTripIds = createdIds
        };
    }

    private async Task<IReadOnlyList<RecurrencePreviewItemResponse>> BuildPreviewItemsAsync(
        TourRecurrenceRule rule,
        DateOnly? requestedFrom,
        DateOnly? requestedTo,
        CancellationToken cancellationToken)
    {
        var effectiveFrom = requestedFrom.HasValue && requestedFrom.Value > rule.StartDate
            ? requestedFrom.Value
            : rule.StartDate;

        var defaultEnd = rule.EndDate >= rule.StartDate ? rule.EndDate : effectiveFrom.AddMonths(3);
        var effectiveTo = requestedTo.HasValue && requestedTo.Value < defaultEnd
            ? requestedTo.Value
            : defaultEnd;

        if (effectiveTo < effectiveFrom)
            return [];

        var candidateDates = ComputeCandidateDates(rule, effectiveFrom, effectiveTo);
        if (candidateDates.Count == 0)
            return [];

        var durationMinutes = rule.Tour.DurationMinutes > 0
            ? rule.Tour.DurationMinutes
            : Math.Max(1, rule.Tour.DurationDays) * 1440;

        var minStartUtc = new DateTimeOffset(candidateDates.First().ToDateTime(rule.StartTime), TimeSpan.Zero).AddHours(-1);
        var maxStartUtc = new DateTimeOffset(candidateDates.Last().ToDateTime(rule.StartTime), TimeSpan.Zero).AddHours(1);

        var existingTrips = await unitOfWork.TourRecurrenceRules.GetTripsByTourAndRangeAsync(
            rule.TourId,
            minStartUtc,
            maxStartUtc,
            cancellationToken);

        var results = new List<RecurrencePreviewItemResponse>(candidateDates.Count);
        foreach (var date in candidateDates)
        {
            var startAt = new DateTimeOffset(date.ToDateTime(rule.StartTime), TimeSpan.Zero);
            var endAt = startAt.AddMinutes(durationMinutes);

            var existing = existingTrips.FirstOrDefault(t =>
                Math.Abs((t.StartAt - startAt).TotalMinutes) < 1);

            results.Add(new RecurrencePreviewItemResponse
            {
                StartAt = startAt,
                EndAt = endAt,
                IsAlreadyGenerated = existing is not null,
                ExistingTripId = existing?.TripId
            });
        }

        return results;
    }

    private static List<DateOnly> ComputeCandidateDates(
        TourRecurrenceRule rule,
        DateOnly effectiveFrom,
        DateOnly effectiveTo)
    {
        var results = new List<DateOnly>();
        var interval = Math.Max(1, rule.IntervalValue);
        var allowedDays = ParseDaysOfWeek(rule.DaysOfWeek, rule.StartDate.DayOfWeek);

        for (var current = effectiveFrom; current <= effectiveTo; current = current.AddDays(1))
        {
            if (current < rule.StartDate)
                continue;

            var daysSinceStart = current.DayNumber - rule.StartDate.DayNumber;

            if (string.Equals(rule.Frequency, "Daily", StringComparison.OrdinalIgnoreCase))
            {
                if (daysSinceStart % interval == 0)
                    results.Add(current);
            }
            else if (string.Equals(rule.Frequency, "Weekly", StringComparison.OrdinalIgnoreCase))
            {
                var weeksSinceStart = daysSinceStart / 7;
                if (weeksSinceStart % interval == 0 && allowedDays.Contains(current.DayOfWeek))
                    results.Add(current);
            }
            else if (string.Equals(rule.Frequency, "Monthly", StringComparison.OrdinalIgnoreCase))
            {
                var monthsSinceStart = (current.Year - rule.StartDate.Year) * 12 + (current.Month - rule.StartDate.Month);
                var targetDay = Math.Min(rule.StartDate.Day, DateTime.DaysInMonth(current.Year, current.Month));
                if (monthsSinceStart >= 0 && monthsSinceStart % interval == 0 && current.Day == targetDay)
                    results.Add(current);
            }

            if (results.Count >= 366)
                break;
        }

        return results;
    }

    private static HashSet<DayOfWeek> ParseDaysOfWeek(string? rawDays, DayOfWeek fallbackDay)
    {
        var set = new HashSet<DayOfWeek>();
        if (string.IsNullOrWhiteSpace(rawDays))
        {
            set.Add(fallbackDay);
            return set;
        }

        var parts = rawDays.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var num))
            {
                var normalized = num == 7 ? 0 : num;
                if (normalized is >= 0 and <= 6)
                    set.Add((DayOfWeek)normalized);
                continue;
            }

            if (Enum.TryParse<DayOfWeek>(part, true, out var dow))
            {
                set.Add(dow);
                continue;
            }

            var mapped = part.ToLowerInvariant() switch
            {
                "mon" or "t2" => (DayOfWeek?)DayOfWeek.Monday,
                "tue" or "t3" => DayOfWeek.Tuesday,
                "wed" or "t4" => DayOfWeek.Wednesday,
                "thu" or "t5" => DayOfWeek.Thursday,
                "fri" or "t6" => DayOfWeek.Friday,
                "sat" or "t7" => DayOfWeek.Saturday,
                "sun" or "cn" => DayOfWeek.Sunday,
                _ => null
            };

            if (mapped.HasValue)
                set.Add(mapped.Value);
        }

        if (set.Count == 0)
            set.Add(fallbackDay);

        return set;
    }

    private static void ValidateRuleInput(
        string frequency,
        int intervalValue,
        string startTime,
        DateOnly startDate,
        DateOnly? endDate,
        int defaultTotalSlots,
        decimal? defaultPrice,
        out TimeOnly parsedStartTime,
        out string normalizedFrequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
            throw new BadRequestException("Tần suất lặp là bắt buộc", "VALIDATION_ERROR");

        normalizedFrequency = frequency.Trim() switch
        {
            var f when f.Equals("Daily", StringComparison.OrdinalIgnoreCase) => "Daily",
            var f when f.Equals("Weekly", StringComparison.OrdinalIgnoreCase) => "Weekly",
            var f when f.Equals("Monthly", StringComparison.OrdinalIgnoreCase) => "Monthly",
            _ => throw new BadRequestException("Tần suất lặp chỉ chấp nhận Daily, Weekly hoặc Monthly", "VALIDATION_ERROR")
        };

        if (intervalValue < 1)
            throw new BadRequestException("Khoảng cách lặp phải lớn hơn hoặc bằng 1", "VALIDATION_ERROR");

        if (!TimeOnly.TryParse(startTime, out parsedStartTime))
            throw new BadRequestException("Giờ khởi hành không đúng định dạng HH:mm:ss", "VALIDATION_ERROR");

        if (endDate.HasValue && endDate.Value < startDate)
            throw new BadRequestException("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu", "VALIDATION_ERROR");

        if (defaultTotalSlots < 1)
            throw new BadRequestException("Tổng số chỗ mặc định phải lớn hơn hoặc bằng 1", "VALIDATION_ERROR");

        if (defaultPrice.HasValue && defaultPrice.Value < 0)
            throw new BadRequestException("Giá mặc định không được âm", "VALIDATION_ERROR");
    }

    private static bool IsTourPublishable(string? status)
        => string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

    private static RecurrenceRuleResponse MapRuleResponse(TourRecurrenceRule rule)
        => new()
        {
            RecurrenceRuleId = rule.RecurrenceRuleId,
            TourId = rule.TourId,
            Frequency = rule.Frequency,
            IntervalValue = rule.IntervalValue,
            DaysOfWeek = rule.DaysOfWeek,
            StartTime = rule.StartTime.ToString("HH:mm:ss"),
            StartDate = rule.StartDate,
            EndDate = rule.EndDate,
            DefaultTotalSlots = rule.DefaultTotalSlots,
            DefaultPrice = rule.DefaultPrice,
            IsActive = rule.IsActive,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
}
