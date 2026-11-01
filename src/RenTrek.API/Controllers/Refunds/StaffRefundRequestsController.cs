using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Services.Implementations.Refunds;

namespace RenTrek.API.Controllers.Refunds;

[ApiController, Authorize(Roles = RoleNames.Staff), Route("api/v1/staff/refund-requests")]
public sealed class StaffRefundRequestsController(StaffRefundRequestService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RefundRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResponse<RefundRequestResponse>>.Ok(await service.ListAsync(status, page, pageSize, ct)));
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(ApiResponse<RefundRequestResponse>.Ok(await service.GetAsync(id, ct)));
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<RefundRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Ok(ApiResponse<RefundRequestResponse>.Ok(await service.ApproveAsync(id, ct)));
    [HttpPost("{id:guid}/reject")]
    [RequestSizeLimit(32768)]
    [ProducesResponseType(typeof(ApiResponse<RefundRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRefundRequest request, CancellationToken ct)
        => Ok(ApiResponse<RefundRequestResponse>.Ok(await service.RejectAsync(id, request.Reason, ct)));
    [HttpPost("{id:guid}/done")]
    [RequestSizeLimit(32768)]
    [ProducesResponseType(typeof(ApiResponse<RefundRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Done(Guid id, [FromBody] CompleteRefundRequest request, CancellationToken ct)
        => Ok(ApiResponse<RefundRequestResponse>.Ok(await service.CompleteAsync(id, request, Request.Headers["Idempotency-Key"].ToString(), ct)));
}
