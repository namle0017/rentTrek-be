using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Interfaces;

public interface ITokenService
{
    (string Token, string Jti, DateTime ExpiresAt) GenerateAccessToken(Account account);
    string GenerateRefreshToken();
    string HashToken(string token);
}
