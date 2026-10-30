using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IUserExternalLoginRepository : IGenericRepository<UserExternalLogin>
{
    Task<UserExternalLogin?> GetByProviderAndSubjectAsync(string provider, string providerSubject, bool tracking = false, CancellationToken cancellationToken = default);
}
