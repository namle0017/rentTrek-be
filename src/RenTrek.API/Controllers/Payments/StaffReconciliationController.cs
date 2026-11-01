using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Payment.Responses;
using RenTrek.BLL.Services.Implementations.Payments;

namespace RenTrek.API.Controllers.Payments;

[ApiController, Authorize(Roles = RoleNames.Staff), Route("api/v1/staff/payment-reconciliation")]
public sealed class StaffReconciliationController(StaffReconciliationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResponse<ReconciliationItemResponse>>.Ok(await service.ListAsync(page, pageSize, ct)));
    [HttpGet("receipts/{receiptId:guid}")]
    public async Task<IActionResult> Detail(Guid receiptId, CancellationToken ct)
        => Ok(ApiResponse<BLL.DTOs.Payment.Requests.SePayWebhookRequest>.Ok(await service.GetReceiptAsync(receiptId, ct)));
    [HttpPost("transactions/{transactionId:guid}/match")]
    public async Task<IActionResult> Match(Guid transactionId, [FromBody] MatchPaymentRequest request, CancellationToken ct)
    {
        await service.MatchAsync(transactionId, request.PaymentId, request.Reason, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }
    [HttpPost("orders/{orderId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid orderId, CancellationToken ct)
    {
        await service.AcceptAsync(orderId, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }
}

public sealed class MatchPaymentRequest
{
    public Guid PaymentId { get; set; }
    public string Reason { get; set; } = "";
}
