using System.Security.Cryptography;
using System.Text;
using RenTrek.BLL.Helpers.Interfaces;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Tạo mã mời participant và băm mã trước khi lưu trữ.
/// </summary>
public sealed class InviteTokenHelper : IInviteTokenHelper
{
    public string CreateCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
