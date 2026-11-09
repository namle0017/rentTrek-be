using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers.Interfaces;

public interface IJwtHelper
{
    (string Token, string Jti, DateTime ExpiresAt) GenerateAccessToken(Account account);
    string GenerateRefreshToken();
    string HashToken(string token);
}
