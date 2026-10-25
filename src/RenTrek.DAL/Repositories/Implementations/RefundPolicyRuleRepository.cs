using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RefundPolicyRuleRepository : GenericRepository<RefundPolicyRule>, IRefundPolicyRuleRepository
{
    public RefundPolicyRuleRepository(AppDbContext context) : base(context)
    {
    }
}
