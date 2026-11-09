using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RefundPolicyRuleRepository : GenericRepository<RefundPolicyRule>, IRefundPolicyRuleRepository
{
    public RefundPolicyRuleRepository(AppDbContext context) : base(context)
    {
    }

    public Task<List<RefundPolicyRule>> GetByPolicyForUpdateAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
        => _dbSet.Where(r => r.RefundPolicyId == refundPolicyId && !r.IsDeleted).ToListAsync(cancellationToken);
}
