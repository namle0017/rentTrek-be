using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TripRepository : GenericRepository<Trip>, ITripRepository
{
    public TripRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Trip?> GetTripWithTourAndRequirementsAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await _context.Trips
            .Where(t => t.TripId == tripId && !t.IsDeleted)
            .Include(t => t.Tour)
                .ThenInclude(tour => tour.ProvidedItems)
                    .ThenInclude(tpi => tpi.ProductVariant)
                        .ThenInclude(pv => pv!.Product)
            .Include(t => t.BookingParticipants.Where(bp => !bp.IsDeleted))
            .Include(t => t.TripGroups.Where(tg => !tg.IsDeleted))
            .Include(t => t.TripProvidedItems)
                .ThenInclude(tpi => tpi.AssetAssignments)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
