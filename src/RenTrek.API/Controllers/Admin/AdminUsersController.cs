using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminUsersController(IAdminService service) : ControllerBase
{

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetUsersAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPost("staff")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateStaffAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(result, "Tạo thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Detail(Guid userId, CancellationToken cancellationToken)
    {
        var result = await service.GetUserAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPut("{userId:guid}/profile")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Profile(Guid userId, [FromBody] AdminProfileRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateUserProfileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPatch("{userId:guid}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Role(Guid userId, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        await service.ChangeRoleAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPost("{userId:guid}/lock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Lock(Guid userId, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        await service.SetUserLockAsync(userId, true, request.Reason, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPost("{userId:guid}/unlock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Unlock(Guid userId, CancellationToken cancellationToken)
    {
        await service.SetUserLockAsync(userId, false, null, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpGet("{userId:guid}/sessions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Sessions(Guid userId, CancellationToken cancellationToken)
    {
        var result = await service.GetSessionsAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpDelete("{userId:guid}/sessions/{sessionId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeSession(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await service.RevokeSessionsAsync(userId, sessionId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpDelete("{userId:guid}/sessions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeAll(Guid userId, CancellationToken cancellationToken)
    {
        await service.RevokeSessionsAsync(userId, null, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpGet("{userId:guid}/activities")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activities(Guid userId, [FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetAuditLogsAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }
}
