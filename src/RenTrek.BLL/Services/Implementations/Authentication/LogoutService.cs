using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Interfaces.Authentication;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Authentication;

public class LogoutService : ILogoutService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;

    public LogoutService(IUnitOfWork unitOfWork, ITokenService tokenService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var token = await _unitOfWork.RefreshTokens.GetByTokenHashWithAccessTokensAsync(tokenHash, tracking: true, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "User logged out";

            foreach (var accessToken in token.AccessTokens.Where(at => at.RevokedAt == null))
            {
                accessToken.RevokedAt = DateTime.UtcNow;
                accessToken.RevokedReason = "User logged out";
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
