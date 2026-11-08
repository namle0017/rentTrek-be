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
[Route("api/v1/admin/orders")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminOrdersController(IAdminOrderService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminOrderResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetOrdersAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminOrderResponse>>.Ok(result, "Lấy danh sách đơn hàng thành công"));
    }

    [HttpGet("{orderCode}")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailAsync(string orderCode, CancellationToken cancellationToken)
    {
        var result = await service.GetOrderAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<AdminOrderDetailResponse>.Ok(result, "Lấy chi tiết đơn hàng thành công"));
    }

    [HttpPatch("{orderCode}/participants/{participantId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ParticipantAsync(string orderCode, Guid participantId, [FromBody] ParticipantOverrideRequest request, CancellationToken cancellationToken)
    {
        await service.OverrideParticipantAsync(orderCode, participantId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thông tin hành khách thành công"));
    }

    [HttpPost("{orderCode}/manual-payment")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PaymentAsync(string orderCode, [FromBody] ManualPaymentRequest request, CancellationToken cancellationToken)
    {
        await service.ManualPaymentAsync(orderCode, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Ghi nhận thanh toán thủ công thành công"));
    }

    [HttpPost("{orderCode}/cancel-by-admin")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAsync(string orderCode, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        await service.CancelOrderAsync(orderCode, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Hủy đơn hàng thành công"));
    }

    /// <summary>O28 "Chấp nhận khoản tiền": xác nhận đơn cần đối soát và tự sinh lệnh hoàn phần tiền thừa.</summary>
    [HttpPost("{orderId:guid}/reconcile-accept")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReconcileAcceptAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await service.ReconcileAcceptAsync(orderId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đã chấp nhận khoản tiền và xác nhận đơn"));
    }

    /// <summary>O28 "Hoàn toàn bộ": sinh lệnh hoàn toàn bộ tiền thực nhận và đóng đơn chờ thanh toán ở Expired.</summary>
    [HttpPost("{orderId:guid}/reconcile-refund")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReconcileRefundAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await service.ReconcileRefundAsync(orderId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đã tạo lệnh hoàn toàn bộ cho đơn đối soát"));
    }
}
