using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces.Authentication;

namespace RenTrek.API.Controllers.Authentication;

[ApiController]
[Route("api/v1/auth")]
public class RefreshTokenController : ControllerBase
{
    private readonly IRefreshTokenService _refreshTokenService;

    public RefreshTokenController(IRefreshTokenService refreshTokenService)
    {
        _refreshTokenService = refreshTokenService;
    }

    // TODO: implement
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
