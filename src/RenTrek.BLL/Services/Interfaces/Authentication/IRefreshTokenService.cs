using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Services.Interfaces.Authentication;

public interface IRefreshTokenService
{
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}
