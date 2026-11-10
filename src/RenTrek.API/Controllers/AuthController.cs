using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Auth.Requests;
using RenTrek.BLL.DTOs.Auth.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho auth.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
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
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status201Created)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSensitive)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AuthResponse>.Ok(result, "Đăng ký tài khoản thành công", StatusCodes.Status201Created));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSensitive)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng nhập thành công"));
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Làm mới phiên đăng nhập thành công"));
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> LogoutAsync([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đăng xuất thành công"));
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSensitive)]
    public async Task<IActionResult> VerifyEmailAsync([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await _authService.VerifyEmailAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xác thực email thành công"));
    }

    [HttpPost("resend-verification-email")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSensitive)]
    public async Task<IActionResult> ResendVerificationEmailAsync([FromBody] ResendVerificationEmailRequest? request, CancellationToken cancellationToken)
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
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSensitive)]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Nếu email tồn tại trong hệ thống, mã xác thực OTP đã được gửi thành công"));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSensitive)]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới"));
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSensitive)]
    public async Task<IActionResult> GoogleLoginAsync([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.GoogleLoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng nhập Google thành công"));
    }
}
