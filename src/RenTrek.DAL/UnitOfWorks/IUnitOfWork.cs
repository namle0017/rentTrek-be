using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.UnitOfWorks;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IAccessTokenRepository AccessTokens { get; }

    /// <summary>Lấy generic repository cho entity chưa có repository riêng.</summary>
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
