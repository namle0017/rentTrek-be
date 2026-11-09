using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho access token qua EF Core.
/// </summary>
public class AccessTokenRepository : GenericRepository<AccessToken>, IAccessTokenRepository
{
    public AccessTokenRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AccessToken?> GetByJtiWithRefreshTokenAsync(string jti, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(at => at.RefreshToken)
            .FirstOrDefaultAsync(at => at.Jti == jti, cancellationToken);
    }

    public async Task<bool> IsJtiRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(at => at.Jti == jti && at.RevokedAt != null, cancellationToken);
    }

    public Task<bool> IsSessionActiveAsync(Guid userId, string jti, string role, DateTime nowUtc, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().AnyAsync(x => x.UserId == userId && x.Jti == jti && x.RevokedAt == null
            && x.ExpiresAt > nowUtc && !x.Account.IsDeleted && x.Account.Status.ToLower() == "active"
            && x.Account.Role.RoleName == role && !x.Account.Role.IsDeleted && x.RefreshToken.RevokedAt == null,
            cancellationToken);
}
