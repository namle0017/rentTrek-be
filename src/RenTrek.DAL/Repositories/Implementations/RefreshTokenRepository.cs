using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(rt => rt.User)
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

    public async Task<List<RefreshToken>> GetActiveTokensByFamilyIdAsync(string familyId, bool tracking = true, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RefreshToken>> GetOtherActiveTokensAsync(long userId, string? excludeFamilyId, bool tracking = true, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(rt => rt.AccessTokens)
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && (excludeFamilyId == null || rt.FamilyId != excludeFamilyId))
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeFamilyTokensAsync(string familyId, string reason, CancellationToken cancellationToken = default)
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
