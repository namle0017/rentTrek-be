using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminOrdersController(IAdminService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetOrdersAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpGet("{orderCode}")]
    public async Task<IActionResult> Detail(string orderCode, CancellationToken cancellationToken)
    {
        var result = await service.GetOrderAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPatch("{orderCode}/participants/{participantId:guid}")]
    public async Task<IActionResult> Participant(string orderCode, Guid participantId, [FromBody] ParticipantOverrideRequest request, CancellationToken cancellationToken)
    {
        await service.OverrideParticipantAsync(orderCode, participantId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPost("{orderCode}/manual-payment")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Payment(string orderCode, [FromBody] ManualPaymentRequest request, CancellationToken cancellationToken)
    {
        await service.ManualPaymentAsync(orderCode, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPost("{orderCode}/cancel-by-admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancel(string orderCode, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        await service.CancelOrderAsync(orderCode, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }
}
