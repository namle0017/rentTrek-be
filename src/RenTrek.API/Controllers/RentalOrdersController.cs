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
    [Authorize]
    public async Task<IActionResult> Quote([FromBody] RentalQuoteRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.QuoteAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<RentalQuoteResponse>.Ok(result, "Báo giá đơn thuê thành công"));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> Create(
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
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> GetById(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.GetByIdAsync(userId, orderId, cancellationToken);
        return Ok(ApiResponse<RentalOrderDetailResponse>.Ok(result, "Lấy chi tiết đơn thuê thành công"));
    }

    [HttpPost("{orderId:guid}/cancel")]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> CancelUnpaid([FromRoute] Guid orderId, [FromBody] CancelRentalOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _rentalOrderService.CancelUnpaidAsync(_currentUserService.RequiredUserId, orderId, request, cancellationToken);
        return Ok(ApiResponse<RentalOrderDetailResponse>.Ok(result, "Đã hủy đơn thuê chưa thanh toán và giải phóng thiết bị."));
    }
}
