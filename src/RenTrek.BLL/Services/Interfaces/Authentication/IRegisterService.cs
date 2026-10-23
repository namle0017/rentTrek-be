using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Services.Interfaces.Authentication;

public interface IRegisterService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}
