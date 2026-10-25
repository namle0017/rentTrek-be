using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class UserExternalLoginRepository : GenericRepository<UserExternalLogin>, IUserExternalLoginRepository
{
    public UserExternalLoginRepository(AppDbContext context) : base(context)
    {
    }
}
