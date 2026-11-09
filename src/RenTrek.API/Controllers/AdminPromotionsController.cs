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
[Route("api/v1/admin/promotions")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminPromotionsController(IAdminPromotionService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminPromotionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPromotionsAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminPromotionResponse>>.Ok(result, "Lấy danh sách khuyến mãi thành công"));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreatePromotionRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreatePromotionAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(result, "Tạo khuyến mãi thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminPromotionDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetPromotionAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminPromotionDetailResponse>.Ok(result, "Lấy chi tiết khuyến mãi thành công"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdatePromotionRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdatePromotionAsync(id, request, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(result, "Cập nhật khuyến mãi thành công"));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StatusAsync(Guid id, [FromBody] StatusRequest request, CancellationToken cancellationToken)
    {
        await service.SetPromotionStatusAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật trạng thái khuyến mãi thành công"));
    }

    [HttpPut("{id:guid}/tiers")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TiersAsync(Guid id, [FromBody] PromotionTiersRequest request, CancellationToken cancellationToken)
    {
        await service.SetPromotionTiersAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật bậc ưu đãi thành công"));
    }

    [HttpGet("{id:guid}/usage")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminPromotionUsageResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UsageAsync(Guid id, [FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPromotionUsageAsync(id, request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminPromotionUsageResponse>>.Ok(result, "Lấy lịch sử dùng khuyến mãi thành công"));
    }
}
