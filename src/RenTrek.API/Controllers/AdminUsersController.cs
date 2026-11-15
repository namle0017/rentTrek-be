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
[Route("api/v1/admin/users")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminUsersController(IAdminUserService service, IAdminGovernanceService governance) : ControllerBase
{

    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminUserResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetUsersAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminUserResponse>>.Ok(result, "Lấy danh sách người dùng thành công"));
    }

    /// <summary>Hướng dẫn viên Guide đang hoạt động cho dropdown phân công; Admin và Staff.</summary>
    [HttpGet("guides")]
    [ProducesResponseType(typeof(ApiResponse<List<AdminUserResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GuidesAsync(CancellationToken cancellationToken)
    {
        var result = await service.GetGuidesAsync(cancellationToken);
        return Ok(ApiResponse<List<AdminUserResponse>>.Ok(result, "Lấy danh sách hướng dẫn viên thành công"));
    }

    [HttpPost("staff")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateStaffAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AdminUserDetailResponse>.Ok(result, "Tạo tài khoản nhân viên thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{userId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await service.GetUserAsync(userId, cancellationToken);
        return Ok(ApiResponse<AdminUserDetailResponse>.Ok(result, "Lấy chi tiết người dùng thành công"));
    }

    [HttpPut("{userId:guid}/profile")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProfileAsync(Guid userId, [FromBody] AdminProfileRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateUserProfileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật hồ sơ người dùng thành công"));
    }

    [HttpPatch("{userId:guid}/role")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RoleAsync(Guid userId, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        await service.ChangeRoleAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đổi vai trò thành công"));
    }

    [HttpPost("{userId:guid}/lock")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LockAsync(Guid userId, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        await service.SetUserLockAsync(userId, true, request.Reason, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Khóa tài khoản thành công"));
    }

    [HttpPost("{userId:guid}/unlock")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlockAsync(Guid userId, CancellationToken cancellationToken)
    {
        await service.SetUserLockAsync(userId, false, null, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Mở khóa tài khoản thành công"));
    }

    [HttpGet("{userId:guid}/sessions")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<List<AdminSessionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await service.GetSessionsAsync(userId, cancellationToken);
        return Ok(ApiResponse<List<AdminSessionResponse>>.Ok(result, "Lấy danh sách phiên đăng nhập thành công"));
    }

    [HttpDelete("{userId:guid}/sessions/{sessionId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await service.RevokeSessionsAsync(userId, sessionId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Thu hồi phiên đăng nhập thành công"));
    }

    [HttpDelete("{userId:guid}/sessions")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await service.RevokeSessionsAsync(userId, null, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Thu hồi mọi phiên đăng nhập thành công"));
    }

    [HttpGet("{userId:guid}/activities")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminAuditResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivitiesAsync(Guid userId, [FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await governance.GetAuditLogsAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminAuditResponse>>.Ok(result, "Lấy lịch sử hoạt động thành công"));
    }
}
