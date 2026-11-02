using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Định nghĩa dịch vụ quản lý ma trận điểm danh hoạt động tour (Màn O14).
/// </summary>
public interface IAdminTripAttendanceService
{
    Task<TripAttendanceMatrixResponse> GetAttendanceMatrixAsync(
        Guid tripId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default);

    Task<UpdateTripAttendancesResponse> UpdateAttendancesAsync(
        Guid tripId,
        UpdateTripAttendancesRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceAuditHistoryItemResponse>> GetAttendanceHistoryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
