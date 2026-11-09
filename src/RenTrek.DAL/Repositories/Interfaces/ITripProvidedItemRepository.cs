using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITripProvidedItemRepository : IGenericRepository<TripProvidedItem>
{
    Task<List<TripProvidedItem>> GetByTripIdWithDetailsAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<TripProvidedItem?> GetByIdWithDetailsAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken = default);
    Task<List<EquipmentAsset>> GetAvailableAssetsForItemAsync(Guid productVariantId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
