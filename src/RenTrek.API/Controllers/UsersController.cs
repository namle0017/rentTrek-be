using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Order.Requests;
using RenTrek.BLL.DTOs.Order.Responses;
using RenTrek.BLL.DTOs.Users.Requests;
using RenTrek.BLL.DTOs.Users.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho users.
/// </summary>
[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IOrderService _orderService;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(IUserService userService, IOrderService orderService, ICurrentUserService currentUserService)
    {
        _userService = userService;
        _orderService = orderService;
        _currentUserService = currentUserService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _userService.GetProfileAsync(userId, cancellationToken);
        return Ok(ApiResponse<UserDetailResponse>.Ok(result, "Lấy thông tin tài khoản thành công"));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> UpdateCurrentUserAsync([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _userService.UpdateProfileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<UserDetailResponse>.Ok(result, "Cập nhật thông tin tài khoản thành công"));
    }

    [HttpPost("me/avatar")]
    [ProducesResponseType(typeof(ApiResponse<UserAvatarResponse>), StatusCodes.Status200OK)]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatarAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        if (file is null || file.Length == 0)
        {
            throw new ValidationException("Vui lòng chọn một file ảnh để tải lên");
        }

        using var stream = file.OpenReadStream();
        var avatarUrl = await _userService.UploadAvatarAsync(
            userId, stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        return Ok(ApiResponse<UserAvatarResponse>.Ok(new UserAvatarResponse(avatarUrl), "Tải lên ảnh đại diện thành công"));
    }

    [HttpPut("me/password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        await _userService.ChangePasswordAsync(userId, request, _currentUserService.Jti, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đổi mật khẩu thành công"));
    }

    [HttpGet("me/orders")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<MyOrderResponse>>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> GetMyOrdersAsync([FromQuery] MyOrderQueryRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _orderService.GetMyOrdersAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<MyOrderResponse>>.Ok(result, "Lấy danh sách đơn hàng thành công"));
    }
}
