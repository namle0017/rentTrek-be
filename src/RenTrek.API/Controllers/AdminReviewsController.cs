using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.Constants;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/admin/reviews")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminReviewsController(IAdminGovernanceService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetReviewsAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminReviewResponse>>.Ok(result, "Lấy danh sách đánh giá thành công"));
    }

    [HttpPost("{id:guid}/hide")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> HideAsync(Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        await service.SetReviewVisibilityAsync(id, true, request.Reason, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Ẩn đánh giá thành công"));
    }

    [HttpPost("{id:guid}/unhide")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnhideAsync(Guid id, CancellationToken cancellationToken)
    {
        await service.SetReviewVisibilityAsync(id, false, null, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Hiện lại đánh giá thành công"));
    }
}
