using BCrypt.Net;
using RenTrek.BLL.Helpers.Interfaces;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Băm và kiểm tra mật khẩu bằng thuật toán an toàn.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
    }
}
