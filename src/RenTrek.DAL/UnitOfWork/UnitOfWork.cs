using Microsoft.EntityFrameworkCore.Storage;
using RenTrek.DAL.Data;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.UnitOfWork;

public sealed class UnitOfWork(AppDbContext context, ITripBookingRepository tripBookings) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public ITripBookingRepository TripBookings { get; } = tripBookings;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction ??= await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        context.Dispose();
    }
}
