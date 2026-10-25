using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    ITripBookingRepository TripBookings { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
