using BCrypt.Net;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

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
