using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.Constants;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminReportsController(IAdminReportService service) : ControllerBase
{

    [HttpGet("revenue")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<List<AdminRevenueResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevenueAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetRevenueAsync(request, cancellationToken);
        return Ok(ApiResponse<List<AdminRevenueResponse>>.Ok(result, "Lấy báo cáo doanh thu thành công"));
    }

    [HttpGet("trip-occupancy")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<List<AdminOccupancyResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OccupancyAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetOccupancyAsync(request, cancellationToken);
        return Ok(ApiResponse<List<AdminOccupancyResponse>>.Ok(result, "Lấy báo cáo lấp đầy chuyến đi thành công"));
    }

    [HttpGet("asset-utilization")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<List<AdminUtilizationResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UtilizationAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetUtilizationAsync(request, cancellationToken);
        return Ok(ApiResponse<List<AdminUtilizationResponse>>.Ok(result, "Lấy báo cáo sử dụng thiết bị thành công"));
    }

    [HttpGet("refunds-and-charges")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminRefundChargeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefundChargesAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetRefundChargesAsync(request, cancellationToken);
        return Ok(ApiResponse<AdminRefundChargeResponse>.Ok(result, "Lấy báo cáo hoàn tiền và phụ phí thành công"));
    }
}
