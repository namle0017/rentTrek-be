using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalSettlementRepository : GenericRepository<RentalSettlement>, IRentalSettlementRepository
{
    public RentalSettlementRepository(AppDbContext context) : base(context)
    {
    }

    public Task<RentalSettlement?> GetByOrderAsync(Guid rentalOrderId, CancellationToken cancellationToken = default)
        => _context.Set<RentalSettlement>()
            .FirstOrDefaultAsync(s => s.RentalOrderId == rentalOrderId, cancellationToken);

    public Task<RentalSettlement?> GetByExtraPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => _context.Set<RentalSettlement>()
            .FirstOrDefaultAsync(s => s.ExtraPaymentId == paymentId, cancellationToken);
}
