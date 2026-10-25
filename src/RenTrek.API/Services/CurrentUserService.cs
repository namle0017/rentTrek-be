using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RenTrek.BLL.Abstractions;
using RenTrek.BLL.Exceptions;

namespace RenTrek.API.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var value = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new UnauthorizedException("Token không chứa định danh tài khoản hợp lệ");
        }
    }
}
