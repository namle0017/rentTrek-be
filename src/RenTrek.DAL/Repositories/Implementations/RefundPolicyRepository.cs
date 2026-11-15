using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho chính sách hoàn tiền qua EF Core.
/// </summary>
public class RefundPolicyRepository : GenericRepository<RefundPolicy>, IRefundPolicyRepository
{
    private const string ActiveStatus = "Active";
    private const string RentalType = "Rental";

    public RefundPolicyRepository(AppDbContext context) : base(context)
    {
    }

    public Task<RefundPolicy?> GetCurrentAsync(
        string policyType,
        string status,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Include(p => p.RefundPolicyRules.Where(r => !r.IsDeleted).OrderByDescending(r => r.MinHoursBefore))
            .Where(p => !p.IsDeleted
                        && p.PolicyType == policyType
                        && p.Status == status
                        && p.EffectiveFrom <= nowUtc
                        && (p.EffectiveUntil == null || p.EffectiveUntil > nowUtc))
            .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Version).ThenBy(p => p.RefundPolicyId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<(IReadOnlyList<AdminRefundPolicyRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminRefundPolicyFilter filter, CancellationToken cancellationToken = default)
    {
        var now = filter.NowUtc;
        var query = _dbSet.AsNoTracking().Where(p => !p.IsDeleted);
        if (filter.PolicyType != null) query = query.Where(p => p.PolicyType == filter.PolicyType);
        if (filter.LatestOnly)
            // Bản nháp (clone chưa kích hoạt) không "thay thế" bản đang áp dụng → cả hai cùng hiện.
            query = query.Where(p => !_dbSet.Any(o => o.PolicyFamilyId == p.PolicyFamilyId && !o.IsDeleted
                                                      && o.Status != "Draft" && o.Version > p.Version));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(search));
        }

        query = filter.DisplayStatus switch
        {
            "Draft" => query.Where(p => p.Status == "Draft"),
            "Archived" => query.Where(p => p.Status == "Archived"),
            "Scheduled" => query.Where(p => p.Status == ActiveStatus && p.EffectiveFrom > now),
            "Expired" => query.Where(p => p.Status == ActiveStatus && p.EffectiveFrom <= now
                                          && p.EffectiveUntil != null && p.EffectiveUntil <= now),
            "Active" => query.Where(p => p.Status == ActiveStatus && p.EffectiveFrom <= now
                                         && (p.EffectiveUntil == null || p.EffectiveUntil > now)),
            _ => query
        };

        return query
            .SortBy(filter.SortBy, filter.IsDescending, nameof(RefundPolicy.CreatedAt), nameof(RefundPolicy.Name),
                nameof(RefundPolicy.EffectiveFrom), nameof(RefundPolicy.Version))
            .ToPageAsync(filter.PageNumber, filter.PageSize, ToAdminRow, cancellationToken);
    }

    public Task<AdminRefundPolicyRow?> GetAdminRowAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Where(p => p.RefundPolicyId == refundPolicyId && !p.IsDeleted)
            .Select(ToAdminRow)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<List<AdminRefundPolicyRow>> GetFamilyVersionsAsync(Guid policyFamilyId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Where(p => p.PolicyFamilyId == policyFamilyId && !p.IsDeleted)
            .OrderByDescending(p => p.Version)
            .Select(ToAdminRow)
            .ToListAsync(cancellationToken);

    public async Task<(int Trips, int Orders)> GetUsageCountsAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
        => (await _context.Trips.CountAsync(t => t.RefundPolicyId == refundPolicyId, cancellationToken),
            await _context.Orders.CountAsync(o => o.RefundPolicyId == refundPolicyId, cancellationToken));

    public Task<int> CountEffectiveActiveAsync(
        string policyType, DateTime nowUtc, Guid? excludePolicyId, CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(p => !p.IsDeleted
                                  && p.PolicyType == policyType
                                  && p.Status == ActiveStatus
                                  && p.EffectiveFrom <= nowUtc
                                  && (p.EffectiveUntil == null || p.EffectiveUntil > nowUtc)
                                  && (excludePolicyId == null || p.RefundPolicyId != excludePolicyId),
            cancellationToken);

    public async Task<bool> IsLatestVersionAsync(Guid policyFamilyId, int version, CancellationToken cancellationToken = default)
        => !await _dbSet.AnyAsync(p => p.PolicyFamilyId == policyFamilyId && !p.IsDeleted && p.Version > version, cancellationToken);

    public Task<List<RefundPolicy>> GetOpenForActivationAsync(
        Guid policyFamilyId,
        string policyType,
        Guid excludePolicyId,
        DateTime effectiveFrom,
        bool includeOtherFamilies,
        CancellationToken cancellationToken = default)
    {
        var otherRental = includeOtherFamilies && policyType == RentalType;
        return _dbSet
            .Where(p => !p.IsDeleted
                        && p.RefundPolicyId != excludePolicyId
                        && p.Status == ActiveStatus
                        && p.EffectiveFrom <= effectiveFrom
                        && (p.EffectiveUntil == null || p.EffectiveUntil > effectiveFrom)
                        && (p.PolicyFamilyId == policyFamilyId || (otherRental && p.PolicyType == RentalType)))
            .ToListAsync(cancellationToken);
    }

    public Task<RefundPolicy?> GetForUpdateAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
        => _dbSet.SingleOrDefaultAsync(p => p.RefundPolicyId == refundPolicyId && !p.IsDeleted, cancellationToken);

    public Task<RefundPolicy?> GetDetailAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Include(p => p.RefundPolicyRules.Where(r => !r.IsDeleted).OrderByDescending(r => r.MinHoursBefore))
            .SingleOrDefaultAsync(p => p.RefundPolicyId == refundPolicyId && !p.IsDeleted, cancellationToken);

    public async Task<bool> IsInUseAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
        => await _context.Trips.AnyAsync(t => t.RefundPolicyId == refundPolicyId, cancellationToken)
           || await _context.Orders.AnyAsync(o => o.RefundPolicyId == refundPolicyId, cancellationToken);

    public async Task<int> GetMaxVersionAsync(Guid policyFamilyId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(p => p.PolicyFamilyId == policyFamilyId).MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0;

    private Expression<Func<RefundPolicy, AdminRefundPolicyRow>> ToAdminRow => p => new AdminRefundPolicyRow
    {
        RefundPolicyId = p.RefundPolicyId,
        PolicyFamilyId = p.PolicyFamilyId,
        Name = p.Name,
        Description = p.Description,
        PolicyType = p.PolicyType,
        Version = p.Version,
        VersionsCount = _dbSet.Count(o => o.PolicyFamilyId == p.PolicyFamilyId && !o.IsDeleted),
        EffectiveFrom = p.EffectiveFrom,
        EffectiveUntil = p.EffectiveUntil,
        Status = p.Status,
        RulesCount = p.RefundPolicyRules.Count(r => !r.IsDeleted),
        TripsCount = p.Trips.Count(),
        OrdersCount = p.Orders.Count(),
        CreatedByName = p.CreatedByUser.FullName,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
