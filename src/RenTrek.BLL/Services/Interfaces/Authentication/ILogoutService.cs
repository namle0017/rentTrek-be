using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Services.Interfaces.Authentication;

public interface ILogoutService
{
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
