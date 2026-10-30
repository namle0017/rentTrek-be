using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IProfileRepository : IGenericRepository<Profile>
{
    Task<Profile?> GetByUserIdAsync(Guid userId, bool tracking = false, CancellationToken cancellationToken = default);
}
