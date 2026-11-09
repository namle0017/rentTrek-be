using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Định nghĩa truy cập dữ liệu cho điểm danh hoạt động chuyến đi (ActivityAttendance).
/// </summary>
public interface IActivityAttendanceRepository : IGenericRepository<ActivityAttendance>
{
    Task<Trip?> GetTripWithTourAndActivitiesAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingParticipant>> GetTripParticipantsWithGroupsAsync(
        Guid tripId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityAttendance>> GetAttendancesByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityAttendance>> GetAttendancesForUpdateAsync(
        Guid activityId,
        IReadOnlyCollection<Guid> participantIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetAttendanceAuditHistoryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAuditLogByBatchIdAsync(
        Guid tripId,
        string clientBatchId,
        CancellationToken cancellationToken = default);
}
