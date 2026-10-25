namespace RenTrek.BLL.Services.Interfaces;

// Interface o BLL, implement o API (doc tu HttpContext)
public interface ICurrentUserService
{
    long? UserId { get; }
    string? Jti { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    string? UserAgent { get; }
    string? IpAddress { get; }
}
