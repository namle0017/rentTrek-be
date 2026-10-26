using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAccountRepository : IGenericRepository<Account>
{
    Task<Account?> GetByEmailWithRoleAsync(string email, bool tracking = false, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdWithRoleAsync(Guid userId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
}
