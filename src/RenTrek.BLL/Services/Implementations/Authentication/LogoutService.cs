using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Interfaces.Authentication;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Authentication;

public class LogoutService : ILogoutService
{
    private readonly IUnitOfWork _unitOfWork;

    public LogoutService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // TODO: implement
    public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
