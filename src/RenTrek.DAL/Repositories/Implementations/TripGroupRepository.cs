using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho TripGroup qua EF Core.
/// </summary>
public class TripGroupRepository : GenericRepository<TripGroup>, ITripGroupRepository
{
    public TripGroupRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Trip?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default)
        => _context.Trips
            .AsNoTracking()
            .Include(x => x.Tour)
            .FirstOrDefaultAsync(x => x.TripId == tripId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<TripGroup>> GetGroupsWithDetailsByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .AsNoTracking()
            .Include(x => x.GuideUser)
                .ThenInclude(u => u!.Profile)
            .Include(x => x.BookingParticipants.Where(p => !p.IsDeleted && p.Status != "Cancelled"))
                .ThenInclude(p => p.BookingOrder)
                    .ThenInclude(bo => bo.Order)
            .Where(x => x.TripId == tripId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.GroupName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TripGroup>> GetGroupsForUpdateByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(x => x.GuideUser)
                .ThenInclude(u => u!.Profile)
            .Include(x => x.BookingParticipants.Where(p => !p.IsDeleted && p.Status != "Cancelled"))
                .ThenInclude(p => p.BookingOrder)
                    .ThenInclude(bo => bo.Order)
            .Where(x => x.TripId == tripId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<TripGroup?> GetGroupForUpdateAsync(
        Guid tripId,
        Guid groupId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(x => x.GuideUser)
                .ThenInclude(u => u!.Profile)
            .Include(x => x.BookingParticipants.Where(p => !p.IsDeleted && p.Status != "Cancelled"))
            .FirstOrDefaultAsync(
                x => x.TripId == tripId && x.TripGroupId == groupId && !x.IsDeleted,
                cancellationToken);

    public Task<bool> GroupNameExistsInTripAsync(
        Guid tripId,
        string groupName,
        Guid? excludeGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = groupName.Trim().ToLower();
        return _dbSet
            .AsNoTracking()
            .AnyAsync(
                x => x.TripId == tripId
                     && !x.IsDeleted
                     && (excludeGroupId == null || x.TripGroupId != excludeGroupId.Value)
                     && x.GroupName.ToLower() == normalized,
                cancellationToken);
    }

    public Task<Account?> GetGuideWithRoleAndProfileAsync(
        Guid guideUserId,
        CancellationToken cancellationToken = default)
        => _context.Accounts
            .AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.Profile)
            .FirstOrDefaultAsync(x => x.UserId == guideUserId && !x.IsDeleted, cancellationToken);

    public Task<bool> HasOverlappingGuideAssignmentAsync(
        Guid guideUserId,
        Guid currentTripId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .AnyAsync(
                x => x.GuideUserId == guideUserId
                     && !x.IsDeleted
                     && x.TripId != currentTripId
                     && !x.Trip.IsDeleted
                     && x.Trip.Status != "Cancelled"
                     && x.Trip.Status != "Completed"
                     && x.Trip.StartAt < endAt
                     && x.Trip.EndAt > startAt,
                cancellationToken);

    public async Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsWithOrderByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.BookingParticipants
            .AsNoTracking()
            .Include(x => x.BookingOrder)
                .ThenInclude(bo => bo.Order)
            .Where(x => x.TripId == tripId && !x.IsDeleted && x.Status != "Cancelled" && x.BookingOrder.Order.Status != "Cancelled")
            .OrderBy(x => x.BookingOrder.Order.OrderCode)
            .ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsForUpdateByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.BookingParticipants
            .Include(x => x.BookingOrder)
                .ThenInclude(bo => bo.Order)
            .Where(x => x.TripId == tripId && !x.IsDeleted && x.Status != "Cancelled" && x.BookingOrder.Order.Status != "Cancelled")
            .OrderBy(x => x.BookingOrder.Order.OrderCode)
            .ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);

    public Task<BookingParticipant?> GetParticipantForUpdateAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken = default)
        => _context.BookingParticipants
            .Include(x => x.BookingOrder)
                .ThenInclude(bo => bo.Order)
            .FirstOrDefaultAsync(
                x => x.TripId == tripId && x.ParticipantId == participantId && !x.IsDeleted && x.Status != "Cancelled",
                cancellationToken);

    public Task<int> GetGroupParticipantCountAsync(Guid groupId, CancellationToken cancellationToken = default)
        => _context.BookingParticipants
            .AsNoTracking()
            .CountAsync(
                x => x.TripGroupId == groupId && !x.IsDeleted && x.Status != "Cancelled",
                cancellationToken);

    public Task<TripBookingOrder?> GetBookingOrderForUpdateAsync(
        Guid tripId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
        => _context.TripBookingOrders
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.TripId == tripId && x.OrderId == bookingId, cancellationToken);

    public async Task<IReadOnlyList<TripGroup>> GetGuideAssignedGroupsWithTripAndLocationAsync(
        Guid guideUserId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .AsNoTracking()
            .Include(x => x.Trip)
                .ThenInclude(t => t.Tour)
                    .ThenInclude(tour => tour.MainLocation)
            .Include(x => x.BookingParticipants.Where(p => !p.IsDeleted && p.Status != "Cancelled"))
            .Where(x => x.GuideUserId == guideUserId && !x.IsDeleted && !x.Trip.IsDeleted && x.Trip.Status != "Cancelled")
            .OrderBy(x => x.Trip.StartAt)
            .ToListAsync(cancellationToken);

    public Task<TripGroup?> GetGuideGroupForTripAsync(
        Guid tripId,
        Guid guideUserId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Include(x => x.Trip)
                .ThenInclude(t => t.Tour)
                    .ThenInclude(tour => tour.Activities.Where(a => !a.IsDeleted))
            .Include(x => x.BookingParticipants.Where(p => !p.IsDeleted && p.Status != "Cancelled"))
            .FirstOrDefaultAsync(
                x => x.TripId == tripId && x.GuideUserId == guideUserId && !x.IsDeleted && !x.Trip.IsDeleted,
                cancellationToken);
}
