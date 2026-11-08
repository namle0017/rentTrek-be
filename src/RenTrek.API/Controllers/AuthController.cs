using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Auth.Requests;
using RenTrek.BLL.DTOs.Auth.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho auth và bảo mật phiên.
/// Giới hạn tần suất 10 request/phút/IP (Chống Brute-Force & DoS).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitPolicies.AuthPublic)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(IAuthService authService, ICurrentUserService currentUserService)
    {
        _authService = authService;
        _currentUserService = currentUserService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AuthResponse>.Ok(result, "Đăng ký tài khoản thành công", StatusCodes.Status201Created));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng nhập thành công"));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Làm mới phiên đăng nhập thành công"));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đăng xuất thành công"));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await _authService.VerifyEmailAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xác thực email thành công"));
    }

    [HttpPost("resend-verification-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequest? request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request?.Email))
        {
            await _authService.ResendVerificationEmailByEmailAsync(request.Email, cancellationToken);
            return Ok(ApiResponse<object?>.Ok(null, "Đã gửi lại email xác thực"));
        }

        if (_currentUserService.UserId.HasValue)
        {
            await _authService.ResendVerificationEmailAsync(_currentUserService.UserId.Value, cancellationToken);
            return Ok(ApiResponse<object?>.Ok(null, "Đã gửi lại email xác thực"));
        }

        return BadRequest(ApiResponse<object?>.Fail("Vui lòng cung cấp email cần gửi lại mã xác thực", StatusCodes.Status400BadRequest));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Nếu email tồn tại trong hệ thống, mã xác thực OTP đã được gửi thành công"));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới"));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.GoogleLoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng nhập Google thành công"));
    }
}
