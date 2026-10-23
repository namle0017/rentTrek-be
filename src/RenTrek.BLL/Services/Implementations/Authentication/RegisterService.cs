using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Interfaces.Authentication;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Authentication;

public class RegisterService : IRegisterService
{
    private readonly IUnitOfWork _unitOfWork;

    public RegisterService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // TODO: implement
    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
