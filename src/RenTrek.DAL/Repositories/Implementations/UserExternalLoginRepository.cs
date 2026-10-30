using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class UserExternalLoginRepository : GenericRepository<UserExternalLogin>, IUserExternalLoginRepository
{
    public UserExternalLoginRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<UserExternalLogin?> GetByProviderAndSubjectAsync(string provider, string providerSubject, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(x => x.User)
                .ThenInclude(u => u.Role)
            .Include(x => x.User)
                .ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderSubject == providerSubject, cancellationToken);
    }
}
