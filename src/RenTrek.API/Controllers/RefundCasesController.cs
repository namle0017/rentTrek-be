using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
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
    public async Task<IActionResult> GetById(Guid refundCaseId, CancellationToken cancellationToken)
    {
        var result = await refundCaseService.GetAsync(refundCaseId, cancellationToken);
        return Ok(ApiResponse<RefundCaseResponse>.Ok(result, "Lấy hồ sơ hoàn tiền thành công"));
    }
}
