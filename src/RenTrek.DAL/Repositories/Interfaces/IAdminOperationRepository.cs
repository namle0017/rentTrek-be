namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAdminOperationRepository
{
    Task AcquireLockAsync(string key, CancellationToken cancellationToken = default);
}
