using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Implementations.Refunds;

namespace RenTrek.API.Controllers.Refunds;

[ApiController, Authorize(Roles = RoleNames.Customer), Route("api/v1")]
public sealed class CustomerRefundRequestsController(RefundRequestService service) : ControllerBase
{
    [HttpGet("orders/{orderId:guid}/refund-eligibility")]
    [ProducesResponseType(typeof(ApiResponse<RefundEligibilityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(Guid orderId, [FromQuery] string reasonCode, CancellationToken ct)
        => Ok(ApiResponse<RefundEligibilityResponse>.Ok(await service.PreviewAsync(orderId, reasonCode, ct)));
    [HttpPost("orders/{orderId:guid}/refund-requests")]
    [RequestSizeLimit(32768)]
    [ProducesResponseType(typeof(ApiResponse<RefundRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<RefundRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid orderId, [FromBody] CreateRefundRequest request, CancellationToken ct)
    {
        var response = await service.SubmitAsync(orderId, request, Request.Headers["Idempotency-Key"].ToString(), ct);
        if (response.Replayed) return Ok(ApiResponse<RefundRequestResponse>.Ok(response));
        return StatusCode(201, ApiResponse<RefundRequestResponse>.Ok(response, "Đã gửi yêu cầu hoàn tiền.", 201));
    }
    [HttpGet("orders/{orderId:guid}/refund-requests")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RefundRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid orderId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResponse<RefundRequestResponse>>.Ok(await service.ListAsync(orderId, page, pageSize, ct)));
    [HttpGet("refund-requests/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<RefundRequestResponse>.Ok(await service.GetAsync(id, ct)));
}
