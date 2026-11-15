using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho ma trận điểm danh hoạt động tour và lịch sử chỉnh sửa (Màn O14 - Mục 11).
/// Staff chỉ xem; chỉ Admin được ghi/sửa (xử lý ngoại lệ, bắt buộc lý do). HDV điểm danh qua <c>/guide</c>.
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/trips/{tripId:guid}")]
public sealed class AdminTripAttendancesController(
    IAdminTripAttendanceService attendanceService) : ControllerBase
{
    [HttpGet("attendance-matrix")]
    [ProducesResponseType(typeof(ApiResponse<TripAttendanceMatrixResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendanceMatrixAsync(
        Guid tripId,
        [FromQuery] Guid? groupId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetAttendanceMatrixAsync(tripId, groupId, cancellationToken);
        return Ok(ApiResponse<TripAttendanceMatrixResponse>.Ok(result, "Lấy bảng điểm danh thành công"));
    }

    [HttpPut("attendances")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<UpdateTripAttendancesResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAttendancesAsync(
        Guid tripId,
        [FromBody] UpdateTripAttendancesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.UpdateAttendancesAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<UpdateTripAttendancesResponse>.Ok(result, "Cập nhật điểm danh thành công"));
    }

    [HttpGet("attendances/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendanceAuditHistoryItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendanceHistoryAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetAttendanceHistoryAsync(tripId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AttendanceAuditHistoryItemResponse>>.Ok(result, "Lấy lịch sử điểm danh thành công"));
    }
}
