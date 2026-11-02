using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho ActivityAttendance qua EF Core.
/// </summary>
public class ActivityAttendanceRepository : GenericRepository<ActivityAttendance>, IActivityAttendanceRepository
{
    public ActivityAttendanceRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Trip?> GetTripWithTourAndActivitiesAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => _context.Trips
            .AsNoTracking()
            .Include(x => x.Tour)
                .ThenInclude(t => t.Activities.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(x => x.TripId == tripId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<BookingParticipant>> GetTripParticipantsWithGroupsAsync(
        Guid tripId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BookingParticipants
            .AsNoTracking()
            .Include(x => x.TripGroup)
            .Include(x => x.BookingOrder)
                .ThenInclude(bo => bo.Order)
            .Where(x => x.TripId == tripId && !x.IsDeleted && x.Status != "Cancelled" && x.BookingOrder.Order.Status != "Cancelled");

        if (groupId.HasValue && groupId.Value != Guid.Empty)
            query = query.Where(x => x.TripGroupId == groupId.Value);

        return await query
            .OrderBy(x => x.TripGroup != null ? x.TripGroup.GroupName : "ZZZZ")
            .ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityAttendance>> GetAttendancesByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .AsNoTracking()
            .Include(x => x.CheckedByUser)
            .Include(x => x.TourActivity)
            .Include(x => x.Participant)
            .Where(x => x.Participant.TripId == tripId && !x.Participant.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ActivityAttendance>> GetAttendancesForUpdateAsync(
        Guid activityId,
        IReadOnlyCollection<Guid> participantIds,
        CancellationToken cancellationToken = default)
    {
        if (participantIds.Count == 0)
            return [];

        return await _dbSet
            .Include(x => x.Participant)
            .Include(x => x.TourActivity)
            .Where(x => x.TourActivityId == activityId && participantIds.Contains(x.ParticipantId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetAttendanceAuditHistoryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.AuditLogs
            .AsNoTracking()
            .Include(x => x.ActorUser)
            .Where(x => x.EntityId == tripId &&
                        (x.EntityType == "ActivityAttendance" || x.Action == "UPDATE_ATTENDANCE" || x.Action == "GUIDE_BATCH_ATTENDANCE"))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAuditLogByBatchIdAsync(
        Guid tripId,
        string clientBatchId,
        CancellationToken cancellationToken = default)
    {
        var marker = $"[BatchId:{clientBatchId.Trim()}]";
        return _context.AuditLogs
            .AsNoTracking()
            .AnyAsync(
                x => x.EntityId == tripId && x.Details.Contains(marker),
                cancellationToken);
    }
}
