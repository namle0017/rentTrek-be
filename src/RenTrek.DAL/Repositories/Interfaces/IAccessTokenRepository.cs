using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAccessTokenRepository : IGenericRepository<AccessToken>
{
    Task<AccessToken?> GetByJtiWithRefreshTokenAsync(string jti, CancellationToken cancellationToken = default);
    Task<bool> IsJtiRevokedAsync(string jti, CancellationToken cancellationToken = default);
}
