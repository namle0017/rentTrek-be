using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Tự xếp booking vào group sau thanh toán và điều phối group (snapshot, gán guide, chuyển, tách booking).</summary>
public interface ITripGroupingService
{
    Task AutoAssignPaidBookingInTransactionAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> GetGroupingSnapshotAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> AssignGuideAsync(
        Guid tripId,
        Guid groupId,
        AssignTripGroupGuideRequest request,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> MoveBookingAsync(
        Guid tripId,
        Guid bookingOrderId,
        MoveBookingGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> SplitBookingAsync(
        Guid tripId,
        Guid bookingOrderId,
        SplitTripBookingRequest request,
        CancellationToken cancellationToken = default);
}
