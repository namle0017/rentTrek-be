using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces.Authentication;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IRegisterService _registerService;
    private readonly ILoginService _loginService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogoutService _logoutService;

    public AuthController(
        IRegisterService registerService,
        ILoginService loginService,
        IRefreshTokenService refreshTokenService,
        ILogoutService logoutService)
    {
        _registerService = registerService;
        _loginService = loginService;
        _refreshTokenService = refreshTokenService;
        _logoutService = logoutService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _registerService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AuthResponse>.Ok(result, "Đăng ký tài khoản thành công", StatusCodes.Status201Created));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _loginService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng nhập thành công"));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _refreshTokenService.RefreshTokenAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Làm mới phiên đăng nhập thành công"));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await _logoutService.LogoutAsync(request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đăng xuất thành công"));
    }
}
