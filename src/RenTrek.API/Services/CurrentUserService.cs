using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // TODO: đọc claim từ _httpContextAccessor.HttpContext?.User (sub, jti, role)
    public Guid? UserId => throw new NotImplementedException();
    public string? Jti => throw new NotImplementedException();
    public string? Role => throw new NotImplementedException();
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
