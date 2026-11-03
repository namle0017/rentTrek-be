using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Truy cập dữ liệu cho nghiệp vụ chia TripGroup, cấu hình lịch lặp, checklist đồ cấp chuyến và truy vấn của Guide/Staff.
/// </summary>
public interface ITripGroupRepository : IGenericRepository<TripGroup>
{
    Task AcquireTripLockAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task AcquireGuideLockAsync(Guid guideUserId, CancellationToken cancellationToken = default);
    Task AcquireRecurrenceRuleLockAsync(Guid recurrenceRuleId, CancellationToken cancellationToken = default);

    Task<Tour?> GetTourForWriteAsync(Guid tourId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TourRecurrenceRule>> GetRecurrenceRulesByTourAsync(Guid tourId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TourRecurrenceRule>> GetActiveRecurrenceRulesAsync(Guid? tourId = null, CancellationToken cancellationToken = default);

    Task<Guid?> GetActiveTourRefundPolicyIdAsync(CancellationToken cancellationToken = default);
    Task<Guid?> GetDefaultManagerUserIdAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetAdminAccountIdsAsync(CancellationToken cancellationToken = default);

    Task<Trip?> GetTripWorkspaceGraphAsync(Guid tripId, bool asTracking = true, CancellationToken cancellationToken = default);
    Task<Guid?> GetBookingTripIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetBookingWithOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Account?> GetAccountWithRoleAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingGuideAssignmentAsync(
        Guid guideUserId,
        Guid excludeTripId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripGroup>> GetGuideGroupsAsync(Guid guideUserId, CancellationToken cancellationToken = default);
    Task<TripGroup?> GetGuideGroupByIdAsync(Guid groupId, Guid guideUserId, CancellationToken cancellationToken = default);
}
