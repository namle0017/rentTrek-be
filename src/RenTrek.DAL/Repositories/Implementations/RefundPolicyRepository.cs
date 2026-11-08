using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho chính sách hoàn tiền qua EF Core.
/// </summary>
public class RefundPolicyRepository : GenericRepository<RefundPolicy>, IRefundPolicyRepository
{
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
            .OrderByDescending(p => p.Version).ThenByDescending(p => p.EffectiveFrom).ThenBy(p => p.RefundPolicyId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<AdminRefundPolicyRow>> GetAdminListAsync(string? policyType, CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Where(p => !p.IsDeleted && (policyType == null || p.PolicyType == policyType))
            .OrderBy(p => p.PolicyFamilyId).ThenByDescending(p => p.Version)
            .Select(p => new AdminRefundPolicyRow
            {
                RefundPolicyId = p.RefundPolicyId,
                PolicyFamilyId = p.PolicyFamilyId,
                Name = p.Name,
                PolicyType = p.PolicyType,
                Version = p.Version,
                EffectiveFrom = p.EffectiveFrom,
                EffectiveUntil = p.EffectiveUntil,
                Status = p.Status,
                UsageCount = p.Trips.Count() + p.Orders.Count()
            })
            .ToListAsync(cancellationToken);

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
}
