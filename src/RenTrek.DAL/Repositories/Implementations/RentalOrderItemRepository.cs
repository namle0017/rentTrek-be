using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalOrderItemRepository : GenericRepository<RentalOrderItem>, IRentalOrderItemRepository
{
    public RentalOrderItemRepository(AppDbContext context) : base(context)
    {
    }
}
