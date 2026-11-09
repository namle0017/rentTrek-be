using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalChargeRepository : GenericRepository<RentalCharge>, IRentalChargeRepository
{
    public RentalChargeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<RentalCharge>> GetChargesByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return await _context.RentalCharges
            .AsNoTracking()
            .Include(rc => rc.EquipmentAsset)
                .ThenInclude(ea => ea!.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .Include(rc => rc.CreatedByUser)
            .Include(rc => rc.RentalOrder)
                .ThenInclude(ro => ro.Order)
            .Where(rc => rc.RentalOrder.Order.OrderCode == orderCode)
            .OrderByDescending(rc => rc.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<RentalCharge?> GetChargeByIdWithDetailsAsync(Guid chargeId, CancellationToken cancellationToken = default)
    {
        return await _context.RentalCharges
            .Include(rc => rc.EquipmentAsset)
            .Include(rc => rc.CreatedByUser)
            .Include(rc => rc.RentalOrder)
                .ThenInclude(ro => ro.Order)
            .FirstOrDefaultAsync(rc => rc.RentalChargeId == chargeId, cancellationToken);
    }
}
