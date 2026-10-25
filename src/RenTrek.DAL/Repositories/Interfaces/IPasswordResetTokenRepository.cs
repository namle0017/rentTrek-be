using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IPasswordResetTokenRepository : IGenericRepository<PasswordResetToken>
{
    // Query đặc thù của PasswordResetToken khai báo tại đây
}
