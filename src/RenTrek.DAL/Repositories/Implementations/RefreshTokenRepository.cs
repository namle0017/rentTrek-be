using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho refresh token qua EF Core.
/// </summary>
public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(rt => rt.Account)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<RefreshToken?> GetByTokenHashWithAccessTokensAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(rt => rt.AccessTokens)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<List<RefreshToken>> GetOtherActiveTokensAsync(Guid userId, Guid? excludeFamilyId, bool tracking = true, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(rt => rt.AccessTokens)
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && (excludeFamilyId == null || rt.FamilyId != excludeFamilyId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task<RefreshToken?> GetForUserAsync(Guid userId, Guid refreshTokenId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.RefreshTokenId == refreshTokenId, cancellationToken);

    public Task<List<RefreshToken>> GetByUserWithAccessTokensAsync(Guid userId, Guid? familyId, CancellationToken cancellationToken = default)
        => _dbSet.Include(x => x.AccessTokens)
            .Where(x => x.UserId == userId && (!familyId.HasValue || x.FamilyId == familyId)).ToListAsync(cancellationToken);

    public async Task RevokeFamilyTokensAsync(Guid familyId, string reason, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbSet
            .Include(rt => rt.AccessTokens)
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;

            foreach (var at in token.AccessTokens.Where(a => a.RevokedAt == null))
            {
                at.RevokedAt = now;
                at.RevokedReason = reason;
            }
        }
    }
}
