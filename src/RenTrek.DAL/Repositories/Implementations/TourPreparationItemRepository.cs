using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TourPreparationItemRepository : GenericRepository<TourPreparationItem>, ITourPreparationItemRepository
{
    public TourPreparationItemRepository(AppDbContext context) : base(context)
    {
    }
}
