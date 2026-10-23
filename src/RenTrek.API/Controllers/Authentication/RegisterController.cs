using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces.Authentication;

namespace RenTrek.API.Controllers.Authentication;

[ApiController]
[Route("api/v1/auth")]
public class RegisterController : ControllerBase
{
    private readonly IRegisterService _registerService;

    public RegisterController(IRegisterService registerService)
    {
        _registerService = registerService;
    }

    // TODO: implement
    [HttpPost("register")]
    [AllowAnonymous]
    public Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
