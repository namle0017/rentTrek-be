using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailWithRoleAsync(string email, bool tracking = false, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithRoleAsync(long userId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
}
