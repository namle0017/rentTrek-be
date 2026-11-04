using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/promotions")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminPromotionsController(IAdminService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPromotionsAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] PromotionWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SavePromotionAsync(null, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(result, "Tạo thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetPromotionAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PromotionWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SavePromotionAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusRequest request, CancellationToken cancellationToken)
    {
        await service.SetPromotionStatusAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPut("{id:guid}/tiers")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Tiers(Guid id, [FromBody] PromotionTiersRequest request, CancellationToken cancellationToken)
    {
        await service.SetPromotionTiersAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpGet("{id:guid}/usage")]
    public async Task<IActionResult> Usage(Guid id, [FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPromotionUsageAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }
}
