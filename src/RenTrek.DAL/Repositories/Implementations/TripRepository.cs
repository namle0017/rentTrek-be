using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho Trip qua EF Core.
/// </summary>
public class TripRepository : GenericRepository<Trip>, ITripRepository
{
    public TripRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Trip> Items, int TotalCount)> GetPagedAdminTripsAsync(
        Guid? tourId,
        string? status,
        DateTimeOffset? fromStartAt,
        DateTimeOffset? toStartAt,
        Guid? managedByUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(x => x.Tour)
            .Include(x => x.ManagedByUser)
            .Where(x => !x.IsDeleted);

        if (tourId.HasValue && tourId.Value != Guid.Empty)
            query = query.Where(x => x.TourId == tourId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (fromStartAt.HasValue)
            query = query.Where(x => x.StartAt >= fromStartAt.Value);

        if (toStartAt.HasValue)
            query = query.Where(x => x.StartAt <= toStartAt.Value);

        if (managedByUserId.HasValue && managedByUserId.Value != Guid.Empty)
            query = query.Where(x => x.ManagedByUserId == managedByUserId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.StartAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Dictionary<Guid, int>> GetSoldSlotsByTripIdsAsync(
        IReadOnlyCollection<Guid> tripIds,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        if (tripIds.Count == 0)
            return [];

        return await _context.TripBookingOrders
            .AsNoTracking()
            .Where(x => tripIds.Contains(x.TripId))
            .Where(x => x.Order.Status == "Confirmed" ||
                        x.Order.Status == "Completed" ||
                        (x.Order.Status == "PendingPayment" && x.Order.HoldExpiresAt > utcNow))
            .GroupBy(x => x.TripId)
            .Select(g => new { TripId = g.Key, SoldSlots = g.Sum(x => x.ParticipantCount) })
            .ToDictionaryAsync(x => x.TripId, x => x.SoldSlots, cancellationToken);
    }

    public Task<Trip?> GetAdminTripDetailAsync(Guid tripId, CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Include(x => x.Tour)
            .Include(x => x.ManagedByUser)
            .Include(x => x.RefundPolicy)
            .Include(x => x.TripGroups.Where(g => !g.IsDeleted))
            .FirstOrDefaultAsync(x => x.TripId == tripId && !x.IsDeleted, cancellationToken);

    public Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default)
        => _dbSet
            .Include(x => x.Tour)
            .Include(x => x.ManagedByUser)
            .Include(x => x.RefundPolicy)
            .FirstOrDefaultAsync(x => x.TripId == tripId && !x.IsDeleted, cancellationToken);

    public Task<Tour?> GetTourWithProvidedItemsAsync(Guid tourId, CancellationToken cancellationToken = default)
        => _context.Tours
            .AsNoTracking()
            .Include(x => x.ProvidedItems)
            .FirstOrDefaultAsync(x => x.Id == tourId && !x.IsDeleted, cancellationToken);

    public Task<RefundPolicy?> GetActiveRefundPolicyAsync(
        Guid refundPolicyId,
        CancellationToken cancellationToken = default)
        => _context.RefundPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.RefundPolicyId == refundPolicyId && !x.IsDeleted && x.Status == "Active",
                cancellationToken);

    public Task<Account?> GetAccountByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _context.Accounts
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, cancellationToken);

    public async Task<int> GetSoldSlotsCountAsync(
        Guid tripId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
        => await _context.TripBookingOrders
            .AsNoTracking()
            .Where(x => x.TripId == tripId)
            .Where(x => x.Order.Status == "Confirmed" ||
                        x.Order.Status == "Completed" ||
                        (x.Order.Status == "PendingPayment" && x.Order.HoldExpiresAt > utcNow))
            .SumAsync(x => (int?)x.ParticipantCount, cancellationToken) ?? 0;

    public async Task<IReadOnlyList<TripBookingOrder>> GetTripBookingsWithDetailsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.TripBookingOrders
            .AsNoTracking()
            .Include(x => x.Order)
                .ThenInclude(o => o.CustomerUser)
            .Include(x => x.Participants.Where(p => !p.IsDeleted))
                .ThenInclude(p => p.TripGroup)
            .Where(x => x.TripId == tripId && !x.Order.IsDeleted)
            .OrderByDescending(x => x.Order.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TripBookingOrder>> GetTripBookingsForUpdateAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.TripBookingOrders
            .Include(x => x.Order)
            .Include(x => x.Participants.Where(p => !p.IsDeleted))
            .Where(x => x.TripId == tripId && !x.Order.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TripGroup>> GetActiveTripGroupsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.TripGroups
            .AsNoTracking()
            .Include(x => x.GuideUser)
            .Where(x => x.TripId == tripId && !x.IsDeleted)
            .OrderBy(x => x.GroupName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TripGroup>> GetActiveTripGroupsForUpdateAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.TripGroups
            .Where(x => x.TripId == tripId && !x.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsByTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.BookingParticipants
            .AsNoTracking()
            .Include(x => x.TripGroup)
                .ThenInclude(g => g!.GuideUser)
            .Include(x => x.BookingOrder)
                .ThenInclude(bo => bo.Order)
            .Where(x => x.TripId == tripId && !x.IsDeleted && x.Status != "Cancelled")
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TripProvidedItem>> GetTripProvidedItemsWithAssetsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.TripProvidedItems
            .AsNoTracking()
            .Include(x => x.TourProvidedItem)
            .Include(x => x.ProductVariant)
                .ThenInclude(pv => pv.Product)
            .Include(x => x.AssetAssignments)
                .ThenInclude(aa => aa.EquipmentAsset)
            .Where(x => x.TripId == tripId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TripProvidedItem>> GetTripProvidedItemsForUpdateAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
        => await _context.TripProvidedItems
            .Include(x => x.AssetAssignments)
            .Where(x => x.TripId == tripId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetSucceededPaymentsByOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken = default)
    {
        if (orderIds.Count == 0)
            return [];

        return await _context.Payments
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId) && x.Status == "Succeeded" && x.PaymentType != "Refund")
            .ToListAsync(cancellationToken);
    }

    public Task AddPaymentsAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default)
        => _context.Payments.AddRangeAsync(payments, cancellationToken);

    public Task AddTripProvidedItemsAsync(
        IEnumerable<TripProvidedItem> items,
        CancellationToken cancellationToken = default)
        => _context.TripProvidedItems.AddRangeAsync(items, cancellationToken);

    public Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken = default)
        => _context.AuditLogs.AddAsync(log, cancellationToken).AsTask();
}
