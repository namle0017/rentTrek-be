using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.Constants;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminDashboardController(IAdminReportService service) : ControllerBase
{

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardStatsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StatsAsync(CancellationToken cancellationToken)
    {
        var result = await service.GetStatsAsync(cancellationToken);
        return Ok(ApiResponse<AdminDashboardStatsResponse>.Ok(result, "Lấy thống kê tổng quan thành công"));
    }

    [HttpGet("alerts")]
    [ProducesResponseType(typeof(ApiResponse<List<AdminAlertResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AlertsAsync(CancellationToken cancellationToken)
    {
        var result = await service.GetAlertsAsync(cancellationToken);
        return Ok(ApiResponse<List<AdminAlertResponse>>.Ok(result, "Lấy danh sách cảnh báo thành công"));
    }

    [HttpGet("kpis")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminKpisResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> KpisAsync(CancellationToken cancellationToken)
    {
        var result = await service.GetKpisAsync(cancellationToken);
        return Ok(ApiResponse<AdminKpisResponse>.Ok(result, "Lấy chỉ số KPI thành công"));
    }
}
