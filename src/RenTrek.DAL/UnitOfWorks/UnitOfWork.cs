using Microsoft.EntityFrameworkCore.Storage;
using RenTrek.DAL.Data;
using RenTrek.DAL.Repositories.Implementations;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.UnitOfWorks;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    private IUserRepository? _users;
    private IRoleRepository? _roles;
    private IRefreshTokenRepository? _refreshTokens;
    private IAccessTokenRepository? _accessTokens;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IRoleRepository Roles => _roles ??= new RoleRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
    public IAccessTokenRepository AccessTokens => _accessTokens ??= new AccessTokenRepository(_context);

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        if (!_repositories.TryGetValue(typeof(TEntity), out var repository))
        {
            repository = new GenericRepository<TEntity>(_context);
            _repositories[typeof(TEntity)] = repository;
        }

        return (IGenericRepository<TEntity>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction ??= await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Chưa gọi BeginTransactionAsync.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
        GC.SuppressFinalize(this);
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
