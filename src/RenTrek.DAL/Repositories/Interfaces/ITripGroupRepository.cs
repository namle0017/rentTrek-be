using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
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
