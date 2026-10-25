using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

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
}
