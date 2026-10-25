using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class PromotionTierRepository : GenericRepository<PromotionTier>, IPromotionTierRepository
{
    public PromotionTierRepository(AppDbContext context) : base(context)
    {
    }
}
