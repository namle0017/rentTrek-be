using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface ITripProvidedItemService
{
    Task<TripProvidedItemListResponse> GetProvidedItemsByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<List<AvailableAssetResponse>> GetAvailableAssetsForItemAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken = default);
    Task<bool> AssignAssetsToTripItemAsync(Guid tripId, Guid itemId, AssignTripAssetsRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> UnassignAssetFromTripAsync(Guid tripId, Guid assignmentId, CancellationToken cancellationToken = default);
    Task<int> IssueTripGearAsync(Guid tripId, IssueTripGearRequest request, CancellationToken cancellationToken = default);
    Task<int> ReturnTripGearAsync(Guid tripId, ReturnTripGearRequest request, CancellationToken cancellationToken = default);
}
