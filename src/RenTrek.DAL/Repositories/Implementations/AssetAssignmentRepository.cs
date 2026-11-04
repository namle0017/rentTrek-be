using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class AssetAssignmentRepository : GenericRepository<AssetAssignment>, IAssetAssignmentRepository
{
    public AssetAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AssetAssignment?> GetAssignmentWithAssetAndTripItemAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        return await _context.AssetAssignments
            .Include(a => a.EquipmentAsset)
            .Include(a => a.TripProvidedItem)
                .ThenInclude(tpi => tpi!.Trip)
            .FirstOrDefaultAsync(a => a.AssetAssignmentId == assignmentId, cancellationToken);
    }

    public async Task<List<AssetAssignment>> GetAssignmentsByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await _context.AssetAssignments
            .Include(a => a.EquipmentAsset)
            .Include(a => a.TripProvidedItem)
            .Where(a => a.TripProvidedItem != null && a.TripProvidedItem.TripId == tripId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AssetAssignment>> GetAssignmentsByAssetCodesAndTripIdAsync(Guid tripId, IEnumerable<string> assetCodes, CancellationToken cancellationToken = default)
    {
        var codes = assetCodes.ToList();
        return await _context.AssetAssignments
            .Include(a => a.EquipmentAsset)
            .Include(a => a.TripProvidedItem)
            .Where(a => a.TripProvidedItem != null
                        && a.TripProvidedItem.TripId == tripId
                        && codes.Contains(a.EquipmentAsset.AssetCode))
            .ToListAsync(cancellationToken);
    }
}
