using System.Security.Cryptography;
using System.Text;
using RenTrek.BLL.Helpers.Interfaces;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Tạo token mời participant và băm token trước khi lưu trữ.
/// </summary>
public sealed class InviteTokenHelper : IInviteTokenHelper
{
    public string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
