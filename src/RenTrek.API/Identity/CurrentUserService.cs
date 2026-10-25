using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Identity;

/// <summary>
/// Đọc định danh tài khoản hiện tại từ các claim đã xác thực.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private string? Subject
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        }
    }

    public Guid? UserId => Guid.TryParse(Subject, out var id) ? id : null;

    public Guid RequiredUserId => UserId
        ?? throw new UnauthorizedException("Token không chứa định danh tài khoản hợp lệ");

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
