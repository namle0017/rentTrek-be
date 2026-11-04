using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminDashboardController(IAdminService service) : ControllerBase
{

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken)
    {
        var result = await service.GetStatsAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(CancellationToken cancellationToken)
    {
        var result = await service.GetAlertsAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpGet("kpis")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Kpis(CancellationToken cancellationToken)
    {
        var result = await service.GetKpisAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }
}
