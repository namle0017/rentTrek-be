using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
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
    [HttpGet("{refundCaseId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundCaseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid refundCaseId, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.GetAsync(refundCaseId, cancellationToken);
        return Ok(ApiResponse<RefundCaseResponse>.Ok(result, "Lấy hồ sơ hoàn tiền thành công"));
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
}
