using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces.Authentication;

namespace RenTrek.API.Controllers.Authentication;

[ApiController]
[Route("api/v1/auth")]
public class LogoutController : ControllerBase
{
    private readonly ILogoutService _logoutService;

    public LogoutController(ILogoutService logoutService)
    {
        _logoutService = logoutService;
    }

    // TODO: implement
    [HttpPost("logout")]
    [Authorize]
    public Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
