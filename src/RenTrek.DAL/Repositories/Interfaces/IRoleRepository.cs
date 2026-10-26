using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRoleRepository : IGenericRepository<Role>
{
    Task<Role?> GetByNameAsync(string roleName, bool tracking = false, CancellationToken cancellationToken = default);
}
