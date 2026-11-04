using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Tạo / sửa Trip kèm group và chuyển trạng thái Open / ClosedForBooking.</summary>
public interface ITripService
{
    Task<AdminTripResponse> GetAdminTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<AdminTripResponse> CreateTripAsync(
        CreateTripRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTripResponse> UpdateTripAsync(
        Guid tripId,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTripResponse> UpdateTripStatusAsync(
        Guid tripId,
        UpdateTripStatusRequest request,
        CancellationToken cancellationToken = default);
}
