using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TripProvidedItemRepository : GenericRepository<TripProvidedItem>, ITripProvidedItemRepository
{
    public TripProvidedItemRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<TripProvidedItem>> GetByTripIdWithDetailsAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await _context.TripProvidedItems
            .AsNoTracking()
            .Where(tpi => tpi.TripId == tripId)
            .Include(tpi => tpi.TourProvidedItem)
            .Include(tpi => tpi.ProductVariant)
                .ThenInclude(pv => pv.Product)
            .Include(tpi => tpi.PreparedByUser)
            .Include(tpi => tpi.AssetAssignments)
                .ThenInclude(a => a.EquipmentAsset)
                    .ThenInclude(ea => ea.ProductVariant)
            .OrderBy(tpi => tpi.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TripProvidedItem?> GetByIdWithDetailsAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken = default)
    {
        return await _context.TripProvidedItems
            .Where(tpi => tpi.TripId == tripId && tpi.TripProvidedItemId == itemId)
            .Include(tpi => tpi.Trip)
            .Include(tpi => tpi.TourProvidedItem)
            .Include(tpi => tpi.ProductVariant)
                .ThenInclude(pv => pv.Product)
            .Include(tpi => tpi.AssetAssignments)
                .ThenInclude(a => a.EquipmentAsset)
                    .ThenInclude(ea => ea.ProductVariant)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<EquipmentAsset>> GetAvailableAssetsForItemAsync(Guid productVariantId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var unavailableAssetIds = _context.AssetAssignments
            .Where(a => (a.Status == "Reserved" || a.Status == "Issued")
                        && !(a.ReservedUntil <= fromDate || a.ReservedFrom >= toDate))
            .Select(a => a.EquipmentAssetId);

        return await _context.EquipmentAssets
            .AsNoTracking()
            .Include(ea => ea.ProductVariant)
            .Where(ea => ea.ProductVariantId == productVariantId
                         && ea.AssetStatus == "Available"
                         && !unavailableAssetIds.Contains(ea.EquipmentAssetId))
            .OrderBy(ea => ea.AssetCode)
            .ToListAsync(cancellationToken);
    }
}
