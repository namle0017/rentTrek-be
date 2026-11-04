using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/reviews")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminReviewsController(IAdminService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetReviewsAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPost("{id:guid}/hide")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Hide(Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        await service.SetReviewVisibilityAsync(id, true, request.Reason, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPost("{id:guid}/unhide")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Unhide(Guid id, CancellationToken cancellationToken)
    {
        await service.SetReviewVisibilityAsync(id, false, null, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }
}
