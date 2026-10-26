using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByTokenHashWithAccessTokensAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetOtherActiveTokensAsync(Guid userId, Guid? excludeFamilyId, bool tracking = true, CancellationToken cancellationToken = default);
    Task RevokeFamilyTokensAsync(Guid familyId, string reason, CancellationToken cancellationToken = default);
}
