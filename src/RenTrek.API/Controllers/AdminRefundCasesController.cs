using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
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
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RefundCaseQueueItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueAsync([FromQuery] RefundCaseQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.GetAdminQueueAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<RefundCaseQueueItemResponse>>.Ok(result, "Lấy danh sách hồ sơ thành công"));
    }

    [HttpPost("export/verification-codes")]
    [ProducesResponseType(typeof(ApiResponse<RefundChallengeResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> RequestExportCodeAsync(CancellationToken cancellationToken)
    {
        var result = await refundCaseService.RequestExportCodeAsync(cancellationToken);
        return Ok(ApiResponse<RefundChallengeResponse>.Ok(result, "Đã gửi mã xác thực tới email của bạn"));
    }

    /// <summary>Xuất Excel chứa số tài khoản rõ; mỗi lần xuất cần OTP mới.</summary>
    [HttpPost("export")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, ExcelContentType)]
    public async Task<IActionResult> ExportAsync([FromBody] RefundExportRequest request, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.ExportAsync(request, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return File(result.Content, ExcelContentType, result.FileName);
    }

    [HttpPost("{refundCaseId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<RefundCaseSummaryResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> RejectAsync(
        Guid refundCaseId,
        [FromBody] RefundDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refundCaseService.RejectAsync(refundCaseId, request, cancellationToken);
        return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã từ chối hồ sơ"));
    }

    /// <summary>Hoàn tất hồ sơ kèm 1-3 ảnh chứng từ bắt buộc (multipart/form-data, field files).</summary>
    [HttpPost("{refundCaseId:guid}/complete")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<RefundCaseSummaryResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> CompleteAsync(
        Guid refundCaseId,
        [FromForm] CompleteRefundRequest request,
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var inputs = ProofFormFiles.ToInputs(files);
        try
        {
            var result = await refundCaseService.CompleteAsync(refundCaseId, request, inputs, cancellationToken);
            return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã hoàn tất hồ sơ"));
        }
        finally
        {
            ProofFormFiles.DisposeAll(inputs);
        }
    }

    /// <summary>Nhập file Excel kết quả chuyển khoản của kế toán; dryRun (mặc định true) chỉ xem trước.</summary>
    [HttpPost("import-result")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<RefundImportResultResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> ImportResultAsync(
        IFormFile? file,
        [FromForm] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("file", "Vui lòng chọn file Excel");
        await using var stream = file.OpenReadStream();
        var result = await refundCaseService.ImportResultAsync(stream, file.FileName, dryRun, cancellationToken);
        return Ok(ApiResponse<RefundImportResultResponse>.Ok(
            result, dryRun ? "Đã kiểm tra file kết quả" : "Đã áp dụng file kết quả"));
    }

    /// <summary>Tải ảnh chứng từ (stream thô, không bọc ApiResponse).</summary>
    [HttpGet("{refundCaseId:guid}/proofs/{proofId:guid}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProofAsync(Guid refundCaseId, Guid proofId, CancellationToken cancellationToken)
    {
        var (content, contentType, _) = await refundCaseService.OpenProofAsync(refundCaseId, proofId, asAdmin: true, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(content, contentType);
    }

    [HttpPost("{refundCaseId:guid}/mark-failed")]
    [ProducesResponseType(typeof(ApiResponse<RefundCaseSummaryResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> MarkFailedAsync(
        Guid refundCaseId,
        [FromBody] RefundDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refundCaseService.MarkFailedAsync(refundCaseId, request, cancellationToken);
        return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã ghi nhận chuyển khoản thất bại"));
    }
}
