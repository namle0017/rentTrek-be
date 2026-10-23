using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Interfaces.Authentication;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Authentication;

public class LoginService : ILoginService
{
    private readonly IUnitOfWork _unitOfWork;

    public LoginService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // TODO: implement
    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
