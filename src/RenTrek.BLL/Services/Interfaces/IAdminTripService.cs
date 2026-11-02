using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Định nghĩa dịch vụ quản lý chuyến đi và điều hành khởi hành cho Admin/Staff (Màn O09, O10, O11).
/// </summary>
public interface IAdminTripService
{
    Task<PagedResponse<AdminTripItemResponse>> GetPagedAsync(
        AdminTripQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTripDetailResponse> CreateAsync(
        CreateAdminTripRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTripDetailResponse> GetByIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<AdminTripDetailResponse> UpdateAsync(
        Guid tripId,
        UpdateAdminTripRequest request,
        CancellationToken cancellationToken = default);

    Task<TripReadinessChecklistResponse> GetReadinessChecklistAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<AdminTripDetailResponse> OpenBookingAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<AdminTripDetailResponse> CloseBookingAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<AdminTripDetailResponse> StartTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<AdminTripDetailResponse> CompleteTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<CancelAdminTripResponse> CancelTripAsync(
        Guid tripId,
        CancelAdminTripRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminTripBookingItemResponse>> GetTripBookingsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<TripManifestResponse> GetManifestAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<(byte[] FileBytes, string FileName)> ExportManifestCsvAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
