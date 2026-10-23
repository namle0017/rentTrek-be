using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return long.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Jti
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                   ?? user?.FindFirst("jti")?.Value;
        }
    }

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
