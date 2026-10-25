using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TripProvidedItemRepository : GenericRepository<TripProvidedItem>, ITripProvidedItemRepository
{
    public TripProvidedItemRepository(AppDbContext context) : base(context)
    {
    }
}
