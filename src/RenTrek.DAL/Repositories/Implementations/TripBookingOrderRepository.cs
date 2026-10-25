using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TripBookingOrderRepository : GenericRepository<TripBookingOrder>, ITripBookingOrderRepository
{
    public TripBookingOrderRepository(AppDbContext context) : base(context)
    {
    }
}
