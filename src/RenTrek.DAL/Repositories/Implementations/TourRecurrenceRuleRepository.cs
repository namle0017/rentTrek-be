using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho quy tắc lặp chuyến của tour qua EF Core.
/// </summary>
public class TourRecurrenceRuleRepository : GenericRepository<TourRecurrenceRule>, ITourRecurrenceRuleRepository
{
    public TourRecurrenceRuleRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Tour?> GetTourByIdAsync(Guid tourId, CancellationToken cancellationToken = default)
        => _context.Tours
            .AsNoTracking()
            .Include(x => x.ProvidedItems)
            .FirstOrDefaultAsync(x => x.Id == tourId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<TourRecurrenceRule>> GetByTourIdAsync(
        Guid tourId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .AsNoTracking()
            .Where(x => x.TourId == tourId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<TourRecurrenceRule?> GetRuleWithTourAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Include(x => x.Tour)
                .ThenInclude(t => t.ProvidedItems)
            .FirstOrDefaultAsync(x => x.RecurrenceRuleId == ruleId && !x.IsDeleted, cancellationToken);

    public Task<TourRecurrenceRule?> GetRuleForUpdateAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .Include(x => x.Tour)
                .ThenInclude(t => t.ProvidedItems)
            .FirstOrDefaultAsync(x => x.RecurrenceRuleId == ruleId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetTripsByTourAndRangeAsync(
        Guid tourId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
        => await _context.Trips
            .AsNoTracking()
            .Where(x => x.TourId == tourId && !x.IsDeleted && x.StartAt >= fromUtc && x.StartAt <= toUtc)
            .OrderBy(x => x.StartAt)
            .ToListAsync(cancellationToken);

    public async Task<RefundPolicy?> GetDefaultOrSpecificRefundPolicyAsync(
        Guid? refundPolicyId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentTourPolicies = _context.RefundPolicies
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == "Active" && x.PolicyType == "Tour"
                        && x.EffectiveFrom <= now && (x.EffectiveUntil == null || x.EffectiveUntil > now));

        if (refundPolicyId.HasValue && refundPolicyId.Value != Guid.Empty)
        {
            return await currentTourPolicies
                .FirstOrDefaultAsync(x => x.RefundPolicyId == refundPolicyId.Value, cancellationToken);
        }

        return await currentTourPolicies
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
