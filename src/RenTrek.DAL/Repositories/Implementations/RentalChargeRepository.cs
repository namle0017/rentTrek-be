using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalChargeRepository : GenericRepository<RentalCharge>, IRentalChargeRepository
{
    public RentalChargeRepository(AppDbContext context) : base(context)
    {
    }
}
