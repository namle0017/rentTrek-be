using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Promotion.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Mã khuyến mãi công khai cho khách chọn ở giỏ hàng / báo giá thuê.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/promotions")]
[Produces("application/json")]
public sealed class PromotionsController(IPromotionService promotionService, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Mã đang chạy cho đơn thuê thiết bị (Active, trong hạn, còn lượt).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PublicPromotionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableAsync(CancellationToken cancellationToken)
    {
        var result = await promotionService.GetAvailableAsync(timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PublicPromotionResponse>>.Ok(result, "Lấy danh sách mã khuyến mãi thành công"));
    }
}
