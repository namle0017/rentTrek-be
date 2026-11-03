using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cho Admin xem hồ sơ hoàn tiền, xuất Excel cho kế toán và ghi nhận kết quả chuyển khoản.
/// </summary>
[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/v1/admin/refund-cases")]
public sealed class AdminRefundCasesController(IRefundCaseService refundCaseService) : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> GetQueue([FromQuery] RefundCaseQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.GetAdminQueueAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<RefundCaseQueueItemResponse>>.Ok(result, "Lấy danh sách hồ sơ thành công"));
    }

    [HttpPost("export/verification-codes")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> RequestExportCode(CancellationToken cancellationToken)
    {
        var result = await refundCaseService.RequestExportCodeAsync(cancellationToken);
        return Ok(ApiResponse<RefundChallengeResponse>.Ok(result, "Đã gửi mã xác thực tới email của bạn"));
    }

    /// <summary>Xuất Excel chứa số tài khoản rõ; mỗi lần xuất cần OTP mới.</summary>
    [HttpPost("export")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> Export([FromBody] RefundExportRequest request, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.ExportAsync(request, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return File(result.Content, ExcelContentType, result.FileName);
    }

    [HttpPost("{refundCaseId:guid}/reject")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> Reject(
        Guid refundCaseId,
        [FromBody] RefundDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refundCaseService.RejectAsync(refundCaseId, request, cancellationToken);
        return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã từ chối hồ sơ"));
    }

    [HttpPost("{refundCaseId:guid}/complete")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> Complete(
        Guid refundCaseId,
        [FromBody] CompleteRefundRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refundCaseService.CompleteAsync(refundCaseId, request, cancellationToken);
        return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã hoàn tất hồ sơ"));
    }

    [HttpPost("{refundCaseId:guid}/mark-failed")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> MarkFailed(
        Guid refundCaseId,
        [FromBody] RefundDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refundCaseService.MarkFailedAsync(refundCaseId, request, cancellationToken);
        return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã ghi nhận chuyển khoản thất bại"));
    }
}
