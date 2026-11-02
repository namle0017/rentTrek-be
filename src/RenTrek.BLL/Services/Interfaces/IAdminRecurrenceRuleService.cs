using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Định nghĩa dịch vụ quản lý lịch lặp và tự động sinh chuyến đi cho tour (Màn O08).
/// </summary>
public interface IAdminRecurrenceRuleService
{
    Task<IReadOnlyList<RecurrenceRuleResponse>> GetRulesByTourAsync(
        Guid tourId,
        CancellationToken cancellationToken = default);

    Task<RecurrenceRuleResponse> CreateRuleAsync(
        Guid tourId,
        CreateRecurrenceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<RecurrenceRuleResponse> UpdateRuleAsync(
        Guid ruleId,
        UpdateRecurrenceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteRuleAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurrencePreviewItemResponse>> PreviewTripsAsync(
        Guid ruleId,
        PreviewRecurrenceTripsRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<GenerateRecurrenceTripsResponse> GenerateTripsAsync(
        Guid ruleId,
        GenerateRecurrenceTripsRequest? request = null,
        CancellationToken cancellationToken = default);
}
