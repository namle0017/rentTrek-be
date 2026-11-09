using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Cấu hình chia nhóm của Tour, lịch lặp và sinh Trip tự động từ lịch lặp.</summary>
public interface ITripRecurrenceService
{
    Task<TourGroupSettingsResponse> UpdateTourGroupSettingsAsync(
        Guid tourId,
        UpdateTourGroupSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<TourRecurrenceRuleResponse> CreateRecurrenceRuleAsync(
        Guid tourId,
        CreateTourRecurrenceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TourRecurrenceRuleResponse>> GetRecurrenceRulesAsync(
        Guid tourId,
        CancellationToken cancellationToken = default);

    Task<GenerateRecurringTripsResponse> GenerateRecurringTripsAsync(
        GenerateRecurringTripsRequest request,
        CancellationToken cancellationToken = default);
}
