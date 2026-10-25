using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Users;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(IUserService userService, ICurrentUserService currentUserService)
    {
        _userService = userService;
        _currentUserService = currentUserService;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _userService.GetProfileAsync(userId, cancellationToken);
        return Ok(ApiResponse<UserDetailResponse>.Ok(result, "Lấy thông tin tài khoản thành công"));
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _userService.UpdateProfileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<UserDetailResponse>.Ok(result, "Cập nhật thông tin tài khoản thành công"));
    }

    [HttpPut("me/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        await _userService.ChangePasswordAsync(userId, request, _currentUserService.Jti, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đổi mật khẩu thành công"));
    }
}
