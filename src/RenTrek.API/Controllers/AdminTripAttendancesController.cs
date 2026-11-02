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
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff},{RoleNames.Guide}")]
[Route("api/v1/admin/trips/{tripId:guid}")]
public sealed class AdminTripAttendancesController(
    IAdminTripAttendanceService attendanceService) : ControllerBase
{
    [HttpGet("attendance-matrix")]
    public async Task<IActionResult> GetAttendanceMatrix(
        Guid tripId,
        [FromQuery] Guid? groupId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetAttendanceMatrixAsync(tripId, groupId, cancellationToken);
        return Ok(ApiResponse<TripAttendanceMatrixResponse>.Ok(result, "Thành công"));
    }

    [HttpPut("attendances")]
    public async Task<IActionResult> UpdateAttendances(
        Guid tripId,
        [FromBody] UpdateTripAttendancesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.UpdateAttendancesAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<UpdateTripAttendancesResponse>.Ok(result, "Cập nhật điểm danh thành công"));
    }

    [HttpGet("attendances/history")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> GetAttendanceHistory(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetAttendanceHistoryAsync(tripId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AttendanceAuditHistoryItemResponse>>.Ok(result, "Thành công"));
    }
}
