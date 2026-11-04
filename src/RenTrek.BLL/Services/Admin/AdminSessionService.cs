using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Admin;

public sealed class AdminSessionService(IUnitOfWork unitOfWork, TimeProvider timeProvider) : IAdminSessionService
{
    public Task<bool> IsSessionValidAsync(Guid userId, string jti, string role, CancellationToken cancellationToken = default)
        => unitOfWork.AccessTokens.Query().AnyAsync(x => x.UserId == userId && x.Jti == jti && x.RevokedAt == null
            && x.ExpiresAt > timeProvider.GetUtcNow().UtcDateTime && !x.Account.IsDeleted && x.Account.Status.ToLower() == "active"
            && x.Account.Role.RoleName == role && !x.Account.Role.IsDeleted && x.RefreshToken.RevokedAt == null,
            cancellationToken);
}
