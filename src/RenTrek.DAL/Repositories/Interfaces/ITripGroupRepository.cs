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
/// Định nghĩa truy cập dữ liệu cho nhóm đoàn (TripGroup), bảng Kanban chia nhóm và phân công Hướng dẫn viên.
/// </summary>
public interface ITripGroupRepository : IGenericRepository<TripGroup>
{
    Task<Trip?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripGroup>> GetGroupsWithDetailsByTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripGroup>> GetGroupsForUpdateByTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<TripGroup?> GetGroupForUpdateAsync(Guid tripId, Guid groupId, CancellationToken cancellationToken = default);
    Task<bool> GroupNameExistsInTripAsync(
        Guid tripId,
        string groupName,
        Guid? excludeGroupId = null,
        CancellationToken cancellationToken = default);
    Task<Account?> GetGuideWithRoleAndProfileAsync(Guid guideUserId, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingGuideAssignmentAsync(
        Guid guideUserId,
        Guid currentTripId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsWithOrderByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsForUpdateByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
    Task<BookingParticipant?> GetParticipantForUpdateAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken = default);
    Task<int> GetGroupParticipantCountAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<TripBookingOrder?> GetBookingOrderForUpdateAsync(
        Guid tripId,
        Guid bookingId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripGroup>> GetGuideAssignedGroupsWithTripAndLocationAsync(
        Guid guideUserId,
        CancellationToken cancellationToken = default);
    Task<TripGroup?> GetGuideGroupForTripAsync(
        Guid tripId,
        Guid guideUserId,
        CancellationToken cancellationToken = default);
}
