using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByTokenHashWithAccessTokensAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetActiveTokensByFamilyIdAsync(string familyId, bool tracking = true, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(long userId, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetOtherActiveTokensAsync(long userId, string? excludeFamilyId, bool tracking = true, CancellationToken cancellationToken = default);
    Task RevokeFamilyTokensAsync(string familyId, string reason, CancellationToken cancellationToken = default);
}
