using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TourImageRepository : GenericRepository<TourImage>, ITourImageRepository
{
    public TourImageRepository(AppDbContext context) : base(context)
    {
    }
}
