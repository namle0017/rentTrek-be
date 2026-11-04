using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IPasswordResetTokenRepository : IGenericRepository<PasswordResetToken>
{
    Task<PasswordResetToken?> GetValidTokenAsync(Guid userId, string tokenHash, CancellationToken cancellationToken = default);
    Task InvalidateTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
