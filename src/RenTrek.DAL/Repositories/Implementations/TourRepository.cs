using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TourRepository : GenericRepository<Tour>, ITourRepository
{
    public TourRepository(AppDbContext context) : base(context)
    {
    }
}
