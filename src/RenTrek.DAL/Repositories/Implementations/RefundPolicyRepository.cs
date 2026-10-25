using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RefundPolicyRepository : GenericRepository<RefundPolicy>, IRefundPolicyRepository
{
    public RefundPolicyRepository(AppDbContext context) : base(context)
    {
    }
}
