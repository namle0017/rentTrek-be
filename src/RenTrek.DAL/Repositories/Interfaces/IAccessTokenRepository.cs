using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAccessTokenRepository : IGenericRepository<AccessToken>
{
    Task<AccessToken?> GetByJtiWithRefreshTokenAsync(string jti, CancellationToken cancellationToken = default);
    Task<bool> IsJtiRevokedAsync(string jti, CancellationToken cancellationToken = default);

    /// <summary>Token còn hiệu lực: chưa thu hồi / hết hạn, refresh token chưa thu hồi, tài khoản active đúng vai trò.</summary>
    Task<bool> IsSessionActiveAsync(Guid userId, string jti, string role, DateTime nowUtc, CancellationToken cancellationToken = default);
}
