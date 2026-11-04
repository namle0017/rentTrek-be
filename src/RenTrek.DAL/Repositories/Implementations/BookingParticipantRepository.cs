using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho BookingParticipant qua EF Core.
/// </summary>
public class BookingParticipantRepository : GenericRepository<BookingParticipant>, IBookingParticipantRepository
{
    public BookingParticipantRepository(AppDbContext context) : base(context)
    {
    }

    public Task<BookingParticipant?> GetParticipantForGuideAsync(
        Guid participantId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Include(x => x.TripGroup)
            .Include(x => x.Trip)
                .ThenInclude(t => t.TripProvidedItems)
                    .ThenInclude(tpi => tpi.ProductVariant)
                        .ThenInclude(pv => pv.Product)
            .Include(x => x.Trip)
                .ThenInclude(t => t.TripProvidedItems)
                    .ThenInclude(tpi => tpi.AssetAssignments)
                        .ThenInclude(aa => aa.EquipmentAsset)
            .Include(x => x.ActivityAttendances)
                .ThenInclude(aa => aa.TourActivity)
            .FirstOrDefaultAsync(x => x.ParticipantId == participantId && !x.IsDeleted, cancellationToken);
}
