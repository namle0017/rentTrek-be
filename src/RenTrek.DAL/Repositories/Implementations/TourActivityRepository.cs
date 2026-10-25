using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TourActivityRepository : GenericRepository<TourActivity>, ITourActivityRepository
{
    public TourActivityRepository(AppDbContext context) : base(context)
    {
    }
}
