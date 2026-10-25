using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalBundleItemSnapshotRepository : GenericRepository<RentalBundleItemSnapshot>, IRentalBundleItemSnapshotRepository
{
    public RentalBundleItemSnapshotRepository(AppDbContext context) : base(context)
    {
    }
}
