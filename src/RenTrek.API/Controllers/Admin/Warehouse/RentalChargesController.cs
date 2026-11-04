using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Warehouse.Interfaces;

namespace RenTrek.API.Controllers.Admin.Warehouse;

/// <summary>
/// Cung cấp các endpoint HTTP xử lý phí phạt bồi thường và nghiệm thu đóng đơn thuê (Mục 16 Contract, Màn O26).
/// </summary>
[ApiController]
public class RentalChargesController : ControllerBase
{
    private readonly IFulfillmentService _fulfillmentService;
    private readonly ICurrentUserService _currentUserService;

    public RentalChargesController(
        IFulfillmentService fulfillmentService,
        ICurrentUserService currentUserService)
    {
        _fulfillmentService = fulfillmentService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// 16.1. Danh sách phí phát sinh của đơn thuê (Màn O26).
    /// </summary>
    [HttpGet("api/v1/admin/rental-orders/by-code/{orderCode}/charges")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<List<RentalChargeDetailResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChargesAsync(
        [FromRoute] string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _fulfillmentService.GetChargesAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<List<RentalChargeDetailResponse>>.Ok(result, "Lấy danh sách phí phát sinh thành công"));
    }

    /// <summary>
    /// 16.2. Lập biên bản phí phát sinh mới (Draft) (Màn O26).
    /// </summary>
    [HttpPost("api/v1/admin/rental-orders/by-code/{orderCode}/charges")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<RentalChargeDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateChargeAsync(
        [FromRoute] string orderCode,
        [FromBody] CreateRentalChargeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;
        var result = await _fulfillmentService.CreateChargeAsync(orderCode, request, userId, cancellationToken);
        return Ok(ApiResponse<RentalChargeDetailResponse>.Ok(result, "Lập biên bản phí phát sinh thành công"));
    }

    /// <summary>
    /// 16.3. Chốt phí phát sinh (Draft -> Assessed) (Màn O26).
    /// </summary>
    [HttpPost("api/v1/admin/charges/{chargeId:guid}/assess")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssessChargeAsync(
        [FromRoute] Guid chargeId,
        CancellationToken cancellationToken)
    {
        var result = await _fulfillmentService.AssessChargeAsync(chargeId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Chốt phí phát sinh thành công"));
    }

    /// <summary>
    /// 16.4. Admin duyệt hủy phí phát sinh (Void) (Màn O26).
    /// </summary>
    [HttpPost("api/v1/admin/charges/{chargeId:guid}/void")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VoidChargeAsync(
        [FromRoute] Guid chargeId,
        [FromBody] VoidRentalChargeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;
        var result = await _fulfillmentService.VoidChargeAsync(chargeId, request, userId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Duyệt hủy phí phạt thành công"));
    }

    /// <summary>
    /// 16.6. Nghiệm thu đóng toàn bộ đơn thuê (Complete Order) (Màn O26).
    /// </summary>
    [HttpPost("api/v1/admin/rental-orders/by-code/{orderCode}/complete")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteRentalOrderAsync(
        [FromRoute] string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _fulfillmentService.CompleteRentalOrderAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Nghiệm thu và đóng đơn thuê thành công"));
    }
}
