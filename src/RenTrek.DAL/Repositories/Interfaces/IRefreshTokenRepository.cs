using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByTokenHashWithAccessTokensAsync(string tokenHash, bool tracking = true, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetOtherActiveTokensAsync(Guid userId, Guid? excludeFamilyId, bool tracking = true, CancellationToken cancellationToken = default);
    /// <summary>Phiên còn hiệu lực của người dùng (chỉ đọc), mới nhất trước.</summary>
    Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Thời điểm đăng nhập (token đầu tiên) của từng family, chỉ đọc.</summary>
    Task<Dictionary<Guid, DateTime>> GetFamilyStartTimesAsync(
        Guid userId, IReadOnlyCollection<Guid> familyIds, CancellationToken cancellationToken = default);

    /// <summary>Một refresh token của người dùng (chỉ đọc).</summary>
    Task<RefreshToken?> GetForUserAsync(Guid userId, Guid refreshTokenId, CancellationToken cancellationToken = default);

    /// <summary>Mọi refresh token (tracking, kèm access token) của người dùng, lọc theo family nếu có.</summary>
    Task<List<RefreshToken>> GetByUserWithAccessTokensAsync(Guid userId, Guid? familyId, CancellationToken cancellationToken = default);

    Task RevokeFamilyTokensAsync(Guid familyId, string reason, CancellationToken cancellationToken = default);
}
