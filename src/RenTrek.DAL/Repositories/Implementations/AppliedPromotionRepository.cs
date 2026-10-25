using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class AppliedPromotionRepository : GenericRepository<AppliedPromotion>, IAppliedPromotionRepository
{
    public AppliedPromotionRepository(AppDbContext context) : base(context)
    {
    }
}
