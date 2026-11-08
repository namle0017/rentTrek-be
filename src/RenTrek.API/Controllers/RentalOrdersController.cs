using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho rental orders.
/// </summary>
[ApiController]
[Route("api/v1/rental-orders")]
public class RentalOrdersController : ControllerBase
{
    private readonly IRentalOrderService _rentalOrderService;
    private readonly ICurrentUserService _currentUserService;

    public RentalOrdersController(IRentalOrderService rentalOrderService, ICurrentUserService currentUserService)
    {
        _rentalOrderService = rentalOrderService;
        _currentUserService = currentUserService;
    }

    [HttpPost("quote")]
    [ProducesResponseType(typeof(ApiResponse<RentalQuoteResponse>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> QuoteAsync([FromBody] RentalQuoteRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.QuoteAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<RentalQuoteResponse>.Ok(result, "Báo giá đơn thuê thành công"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RentalOrderDetailResponse>), StatusCodes.Status201Created)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateRentalOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.CreateAsync(userId, request, idempotencyKey, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<RentalOrderDetailResponse>.Ok(result, "Tạo đơn thuê thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.GetByIdAsync(userId, orderId, cancellationToken);
        return Ok(ApiResponse<RentalOrderDetailResponse>.Ok(result, "Lấy chi tiết đơn thuê thành công"));
    }

    /// <summary>Báo giá gia hạn ngày trả cho đơn thuê của chính khách (FR-R09); không ghi dữ liệu.</summary>
    [HttpPost("{orderId:guid}/extensions/quote")]
    [ProducesResponseType(typeof(ApiResponse<RentalExtensionQuoteResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> QuoteExtensionAsync(
        Guid orderId,
        [FromBody] ExtendRentalQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.QuoteExtensionAsync(userId, orderId, request, cancellationToken);
        return Ok(ApiResponse<RentalExtensionQuoteResponse>.Ok(result, "Báo giá gia hạn đơn thuê thành công"));
    }

    /// <summary>Áp dụng gia hạn khi khách đồng ý đúng số tiền đã báo giá (FR-R09, UAT-19).</summary>
    [HttpPost("{orderId:guid}/extensions")]
    [ProducesResponseType(typeof(ApiResponse<RentalExtensionResultResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> ExtendAsync(
        Guid orderId,
        [FromBody] ExtendRentalOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.ExtendAsync(userId, orderId, request, cancellationToken);
        return Ok(ApiResponse<RentalExtensionResultResponse>.Ok(result, "Gia hạn đơn thuê thành công"));
    }

    /// <summary>Lịch sử điều chỉnh (gia hạn) của đơn thuê thuộc khách.</summary>
    [HttpGet("{orderId:guid}/adjustments")]
    [ProducesResponseType(typeof(ApiResponse<List<RentalOrderAdjustmentResponse>>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> GetAdjustmentsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.GetAdjustmentsAsync(userId, orderId, cancellationToken);
        return Ok(ApiResponse<List<RentalOrderAdjustmentResponse>>.Ok(result, "Lấy lịch sử điều chỉnh đơn thuê thành công"));
    }
}
