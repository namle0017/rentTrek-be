using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
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
}
