using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho TripGroup, lịch lặp sinh Trip, checklist đồ cấp chuyến và truy vấn Guide/Staff qua EF Core.
/// </summary>
public class TripGroupRepository : GenericRepository<TripGroup>, ITripGroupRepository
{
    public TripGroupRepository(AppDbContext context) : base(context)
    {
    }

    public Task AcquireTripLockAsync(Guid tripId, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(_context, $"trip-group:trip:{tripId:N}", cancellationToken);

    public Task AcquireGuideLockAsync(Guid guideUserId, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(_context, $"trip-group:guide:{guideUserId:N}", cancellationToken);

    public Task AcquireRecurrenceRuleLockAsync(Guid recurrenceRuleId, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(_context, $"trip-group:recurrence:{recurrenceRuleId:N}", cancellationToken);

    public Task<Tour?> GetTourForWriteAsync(Guid tourId, CancellationToken cancellationToken = default)
        => _context.Tours
            .Include(t => t.ProvidedItems)
            .SingleOrDefaultAsync(t => t.Id == tourId && !t.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<TourRecurrenceRule>> GetRecurrenceRulesByTourAsync(
        Guid tourId,
        CancellationToken cancellationToken = default)
        => await _context.TourRecurrenceRules
            .AsNoTracking()
            .Where(r => r.TourId == tourId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TourRecurrenceRule>> GetActiveRecurrenceRulesAsync(
        Guid? tourId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TourRecurrenceRules
            .AsSplitQuery()
            .Include(r => r.Tour).ThenInclude(t => t.ProvidedItems)
            .Include(r => r.Trips)
            .Where(r => r.IsActive && !r.IsDeleted && !r.Tour.IsDeleted);

        if (tourId.HasValue)
            query = query.Where(r => r.TourId == tourId.Value);

        return await query
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Guid?> GetActiveTourRefundPolicyIdAsync(CancellationToken cancellationToken = default)
        => _context.RefundPolicies
            .AsNoTracking()
            .Where(p => !p.IsDeleted &&
                        p.Status.ToLower() == "active" &&
                        p.PolicyType.ToUpper() == "TOUR")
            .OrderByDescending(p => p.Version)
            .ThenByDescending(p => p.EffectiveFrom)
            .Select(p => (Guid?)p.RefundPolicyId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> GetDefaultManagerUserIdAsync(CancellationToken cancellationToken = default)
        => _context.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted &&
                        a.Status.ToLower() == "active" &&
                        (a.Role.RoleName == "Admin" || a.Role.RoleName == "Staff"))
            .OrderBy(a => a.CreatedAt)
            .Select(a => (Guid?)a.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetAdminAccountIdsAsync(CancellationToken cancellationToken = default)
        => await _context.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted &&
                        a.Status.ToLower() == "active" &&
                        a.Role.RoleName == "Admin")
            .Select(a => a.UserId)
            .ToListAsync(cancellationToken);

    public Task<Trip?> GetTripWorkspaceGraphAsync(
        Guid tripId,
        bool asTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Trips
            .AsSplitQuery()
            .Include(t => t.Tour).ThenInclude(tour => tour.Activities)
            .Include(t => t.Tour).ThenInclude(tour => tour.ProvidedItems)
            .Include(t => t.TripGroups).ThenInclude(g => g.GuideUser)
            .Include(t => t.TripGroups).ThenInclude(g => g.BookingParticipants).ThenInclude(p => p.BookingOrder).ThenInclude(b => b.Order).ThenInclude(o => o.CustomerUser)
            .Include(t => t.TripBookingOrders).ThenInclude(b => b.Order).ThenInclude(o => o.CustomerUser)
            .Include(t => t.TripBookingOrders).ThenInclude(b => b.Participants).ThenInclude(p => p.TripGroup).ThenInclude(g => g!.GuideUser)
            .Include(t => t.TripProvidedItems).ThenInclude(pi => pi.TourProvidedItem)
            .Where(t => t.TripId == tripId && !t.IsDeleted);

        if (!asTracking)
            query = query.AsNoTracking();

        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Guid?> GetBookingTripIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.TripBookingOrders
            .AsNoTracking()
            .Where(b => b.OrderId == orderId)
            .Select(b => (Guid?)b.TripId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Order?> GetBookingWithOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Orders
            .AsNoTracking()
            .Include(o => o.TripBookingOrder)
            .SingleOrDefaultAsync(o => o.OrderId == orderId && !o.IsDeleted, cancellationToken);

    public Task<Account?> GetAccountWithRoleAsync(Guid userId, CancellationToken cancellationToken = default)
        => _context.Accounts
            .AsNoTracking()
            .Include(a => a.Role)
            .SingleOrDefaultAsync(a => a.UserId == userId && !a.IsDeleted, cancellationToken);

    public Task<bool> HasOverlappingGuideAssignmentAsync(
        Guid guideUserId,
        Guid excludeTripId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default)
        => _context.TripGroups
            .AsNoTracking()
            .AnyAsync(g => !g.IsDeleted
                           && g.GuideUserId == guideUserId
                           && g.TripId != excludeTripId
                           && !g.Trip.IsDeleted
                           && g.Trip.Status != "Cancelled"
                           && g.Trip.StartAt < endAt
                           && startAt < g.Trip.EndAt,
                cancellationToken);

    public async Task<IReadOnlyList<TripGroup>> GetGuideGroupsAsync(
        Guid guideUserId,
        CancellationToken cancellationToken = default)
        => await _context.TripGroups
            .AsNoTracking()
            .AsSplitQuery()
            .Include(g => g.GuideUser)
            .Include(g => g.Trip).ThenInclude(t => t.Tour).ThenInclude(tour => tour.Activities)
            .Include(g => g.BookingParticipants).ThenInclude(p => p.BookingOrder).ThenInclude(b => b.Order)
            .Where(g => !g.IsDeleted
                        && g.GuideUserId == guideUserId
                        && !g.Trip.IsDeleted
                        && g.Trip.Status != "Cancelled")
            .OrderBy(g => g.Trip.StartAt)
            .ThenBy(g => g.GroupName)
            .ToListAsync(cancellationToken);

    public Task<TripGroup?> GetGuideGroupByIdAsync(
        Guid groupId,
        Guid guideUserId,
        CancellationToken cancellationToken = default)
        => _context.TripGroups
            .AsNoTracking()
            .AsSplitQuery()
            .Include(g => g.GuideUser)
            .Include(g => g.Trip).ThenInclude(t => t.Tour).ThenInclude(tour => tour.Activities)
            .Include(g => g.BookingParticipants).ThenInclude(p => p.BookingOrder).ThenInclude(b => b.Order)
            .SingleOrDefaultAsync(g => g.TripGroupId == groupId
                                       && !g.IsDeleted
                                       && g.GuideUserId == guideUserId
                                       && !g.Trip.IsDeleted
                                       && g.Trip.Status != "Cancelled",
                cancellationToken);
}
