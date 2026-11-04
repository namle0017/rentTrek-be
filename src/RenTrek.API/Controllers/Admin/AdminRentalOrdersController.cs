using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/rental-orders")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminRentalOrdersController(IAdminService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetRentalsAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPost("{orderCode}/claim")]
    public async Task<IActionResult> Claim(string orderCode, CancellationToken cancellationToken)
    {
        await service.ClaimRentalAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpGet("{orderCode}")]
    public async Task<IActionResult> Detail(string orderCode, CancellationToken cancellationToken)
    {
        var result = await service.GetRentalAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }
}
