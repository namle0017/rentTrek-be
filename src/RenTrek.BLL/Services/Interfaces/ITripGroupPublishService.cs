using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Tính lại đồ cấp theo group và chốt Published.</summary>
public interface ITripGroupPublishService
{
    Task<RecalculateTripProvidedItemsResponse> RecalculateProvidedItemsAsync(
        Guid tripId,
        PublishTripGroupingRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> PublishGroupsAsync(
        Guid tripId,
        PublishTripGroupingRequest? request = null,
        CancellationToken cancellationToken = default);
}
