using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces.Authentication;

namespace RenTrek.API.Controllers.Authentication;

[ApiController]
[Route("api/v1/auth")]
public class LoginController : ControllerBase
{
    private readonly ILoginService _loginService;

    public LoginController(ILoginService loginService)
    {
        _loginService = loginService;
    }

    // TODO: implement
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
