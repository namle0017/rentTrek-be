using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Services.Interfaces.Authentication;

public interface ILoginService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
