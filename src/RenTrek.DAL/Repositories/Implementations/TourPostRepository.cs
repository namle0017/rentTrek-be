using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TourPostRepository : GenericRepository<TourPost>, ITourPostRepository
{
    public TourPostRepository(AppDbContext context) : base(context)
    {
    }
}
