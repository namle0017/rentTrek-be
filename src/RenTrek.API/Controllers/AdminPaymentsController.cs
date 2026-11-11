using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Sổ giao dịch (O27) và hàng đợi hoàn tiền (O28). Hoàn tiền chạy theo luồng thủ công: duyệt chuyển kế toán,
/// kế toán chuyển khoản ngoài hệ thống rồi Admin đánh dấu đã hoàn kèm mã tham chiếu ngân hàng.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminPaymentsController(IAdminPaymentService service) : ControllerBase
{
    /// <summary>O27: danh sách giao dịch thu / hoàn; Staff chỉ thấy đơn trong phạm vi phụ trách.</summary>
    [HttpGet("payments")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminPaymentListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PaymentsAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPaymentsAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminPaymentListItemResponse>>.Ok(result, "Lấy danh sách giao dịch thành công"));
    }

    /// <summary>O27: thẻ KPI (tổng thu / hoàn thành công, đang chờ), cùng bộ lọc và phạm vi với danh sách giao dịch.</summary>
    [HttpGet("payments/stats")]
    [ProducesResponseType(typeof(ApiResponse<AdminPaymentStatsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PaymentStatsAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPaymentStatsAsync(request, cancellationToken);
        return Ok(ApiResponse<AdminPaymentStatsResponse>.Ok(result, "Lấy thống kê giao dịch thành công"));
    }

    /// <summary>O28: hàng đợi hoàn tiền theo tab PendingApproval | Pending | Failed | NeedsReconciliation.</summary>
    [HttpGet("refunds")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminPaymentListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefundsAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetRefundsAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminPaymentListItemResponse>>.Ok(result, "Lấy danh sách hoàn tiền thành công"));
    }

    /// <summary>Duyệt khoản hoàn PendingApproval sang Pending (chờ kế toán chuyển khoản).</summary>
    [HttpPost("refunds/{paymentId:guid}/approve")]
    [Authorize(Roles = RoleNames.Admin)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    [ProducesResponseType(typeof(ApiResponse<AdminRefundActionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(paymentId, cancellationToken);
        return Ok(ApiResponse<AdminRefundActionResponse>.Ok(result, "Duyệt lệnh hoàn tiền thành công"));
    }

    /// <summary>Đưa khoản hoàn Failed về Pending; tối đa 3 lần.</summary>
    [HttpPost("refunds/{paymentId:guid}/retry")]
    [Authorize(Roles = RoleNames.Admin)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    [ProducesResponseType(typeof(ApiResponse<AdminRefundActionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await service.RetryAsync(paymentId, cancellationToken);
        return Ok(ApiResponse<AdminRefundActionResponse>.Ok(result, "Đã đưa lệnh hoàn tiền về hàng chờ chuyển khoản"));
    }

    /// <summary>Đánh dấu khoản hoàn đã chuyển khoản thủ công, bắt buộc mã tham chiếu ngân hàng.</summary>
    [HttpPost("refunds/{paymentId:guid}/mark-manual")]
    [Authorize(Roles = RoleNames.Admin)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    [ProducesResponseType(typeof(ApiResponse<AdminRefundActionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkManualAsync(Guid paymentId, [FromBody] MarkManualRefundRequest request, CancellationToken cancellationToken)
    {
        var result = await service.MarkManualAsync(paymentId, request, cancellationToken);
        return Ok(ApiResponse<AdminRefundActionResponse>.Ok(result, "Đã ghi nhận hoàn tiền thủ công"));
    }
}
