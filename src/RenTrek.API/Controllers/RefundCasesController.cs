using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp endpoint để khách theo dõi hồ sơ hoàn tiền của mình.
/// </summary>
[ApiController]
[Authorize] // Staff/Admin tự đặt tour cũng là khách của đơn đó; quyền sở hữu do Service kiểm tra
[Route("api/v1/refund-cases")]
public sealed class RefundCasesController(IRefundCaseService refundCaseService) : ControllerBase
{
    /// <summary>Danh sách hồ sơ hoàn tiền của khách đang đăng nhập (status: một hoặc nhiều trạng thái, phân tách bằng dấu phẩy).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RefundCaseResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineAsync([FromQuery] RefundCaseQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.GetMineAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<RefundCaseResponse>>.Ok(result, "Lấy danh sách hoàn tiền thành công"));
    }

    [HttpGet("{refundCaseId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundCaseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid refundCaseId, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.GetAsync(refundCaseId, cancellationToken);
        return Ok(ApiResponse<RefundCaseResponse>.Ok(result, "Lấy hồ sơ hoàn tiền thành công"));
    }

    /// <summary>Khách tải ảnh chứng từ của hồ sơ mình đã hoàn tất; hồ sơ / chứng từ khác trả 404.</summary>
    [HttpGet("{refundCaseId:guid}/proofs/{proofId:guid}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProofAsync(Guid refundCaseId, Guid proofId, CancellationToken cancellationToken)
    {
        var (content, contentType, _) = await refundCaseService.OpenProofAsync(refundCaseId, proofId, asAdmin: false, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(content, contentType);
    }

    /// <summary>Khách nhập tài khoản nhận hoàn cho hồ sơ đang chờ (no-show, hoàn cọc sau khi trả đồ).</summary>
    [HttpPost("{refundCaseId:guid}/bank-info")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    [ProducesResponseType(typeof(ApiResponse<RefundCaseSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitBankInfoAsync(
        Guid refundCaseId,
        [FromBody] SubmitRefundBankInfoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refundCaseService.SubmitBankInfoAsync(refundCaseId, request, cancellationToken);
        return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã ghi nhận tài khoản nhận hoàn"));
    }

    /// <summary>Khách gửi lại yêu cầu hoàn tiền kèm STK mới cho hồ sơ bị từ chối / thất bại.</summary>
    [HttpPost("{refundCaseId:guid}/resubmit")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    [ProducesResponseType(typeof(ApiResponse<RefundCaseSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResubmitAsync(
        Guid refundCaseId,
        [FromBody] SubmitRefundBankInfoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refundCaseService.ResubmitAsync(refundCaseId, request, cancellationToken);
        return Ok(ApiResponse<RefundCaseSummaryResponse>.Ok(result, "Đã gửi lại yêu cầu hoàn tiền"));
    }
}
