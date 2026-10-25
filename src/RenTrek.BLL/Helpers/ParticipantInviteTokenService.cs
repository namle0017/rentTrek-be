using System.Security.Cryptography;
using System.Text;

namespace RenTrek.BLL.Helpers;

public sealed class ParticipantInviteTokenService : IParticipantInviteTokenService
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
