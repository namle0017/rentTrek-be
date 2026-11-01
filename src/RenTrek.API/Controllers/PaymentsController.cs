using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.DTOs.Payment.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho thanh toán đơn (VietQR qua SePay) và webhook của SePay.
/// </summary>
[ApiController]
[Route("api/v1")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUserService;
    private readonly RenTrek.BLL.Services.Implementations.Payments.SePayWebhookService _webhooks;

    public PaymentsController(IPaymentService paymentService, ICurrentUserService currentUserService,
        RenTrek.BLL.Services.Implementations.Payments.SePayWebhookService webhooks)
    {
        _paymentService = paymentService;
        _currentUserService = currentUserService;
        _webhooks = webhooks;
    }

    [HttpPost("orders/{orderId:guid}/payments")]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<PaymentResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> Create(
        Guid orderId,
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;
        request.IdempotencyKey = Request.Headers["Idempotency-Key"].ToString();

        var (payment, created) = await _paymentService.CreateAsync(userId, orderId, request, cancellationToken);
        return created
            ? StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<PaymentResponse>.Ok(payment, "Khởi tạo thanh toán thành công", StatusCodes.Status201Created))
            : Ok(ApiResponse<PaymentResponse>.Ok(payment, "Lấy lần thanh toán đang chờ thành công"));
    }

    [HttpGet("orders/{orderId:guid}/payments/{paymentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentStatusResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> GetStatus(Guid orderId, Guid paymentId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _paymentService.GetStatusAsync(userId, orderId, paymentId, cancellationToken);
        return Ok(ApiResponse<PaymentStatusResponse>.Ok(result, "Lấy trạng thái thanh toán thành công"));
    }

    /// <summary>SePay gọi khi tài khoản có giao dịch; phản hồi <c>{"success": true}</c> để SePay không gửi lại.</summary>
    [HttpPost("payments/webhooks/sepay")]
    [AllowAnonymous]
    [RequestSizeLimit(32768)]
    [Consumes("application/json")]
    public async Task<IActionResult> SePayWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var raw = await reader.ReadToEndAsync(cancellationToken);
        await _webhooks.ReceiveAsync(raw, Request.Headers.Authorization.ToString(),
            Request.Headers["X-SePay-Signature"].ToString(), Request.Headers["X-SePay-Timestamp"].ToString(), cancellationToken);
        return Ok(new { success = true });
    }

}
