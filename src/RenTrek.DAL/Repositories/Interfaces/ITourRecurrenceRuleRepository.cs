using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Định nghĩa truy cập dữ liệu cho quy tắc lặp chuyến của tour (TourRecurrenceRule).
/// </summary>
public interface ITourRecurrenceRuleRepository : IGenericRepository<TourRecurrenceRule>
{
    Task<Tour?> GetTourByIdAsync(Guid tourId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TourRecurrenceRule>> GetByTourIdAsync(Guid tourId, CancellationToken cancellationToken = default);
    Task<TourRecurrenceRule?> GetRuleWithTourAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<TourRecurrenceRule?> GetRuleForUpdateAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trip>> GetTripsByTourAndRangeAsync(
        Guid tourId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
    Task<RefundPolicy?> GetDefaultOrSpecificRefundPolicyAsync(
        Guid? refundPolicyId,
        CancellationToken cancellationToken = default);
}
