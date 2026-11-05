using RenTrek.DAL.UnitOfWorks;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

public sealed class AdminSessionService(IUnitOfWork unitOfWork, TimeProvider timeProvider) : IAdminSessionService
{
    public Task<bool> IsSessionValidAsync(Guid userId, string jti, string role, CancellationToken cancellationToken = default)
        => unitOfWork.AccessTokens.IsSessionActiveAsync(userId, jti, role, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
