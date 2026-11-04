using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminReportsController(IAdminService service) : ControllerBase
{

    [HttpGet("revenue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Revenue([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetRevenueAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpGet("trip-occupancy")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Occupancy([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetOccupancyAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpGet("asset-utilization")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Utilization([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetUtilizationAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpGet("refunds-and-charges")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RefundCharges([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetRefundChargesAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }
}
