using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Tính và kiểm tra token HMAC-SHA256 không trạng thái cho link xem chuyến và xác thực email.
/// </summary>
public sealed class InviteTokenHelper(IOptions<InviteSettings> options) : IInviteTokenHelper
{
    private const int MinKeyLength = 32;

    public string CreateGroupToken(Guid orderId, int version)
        => Sign($"{orderId:N}:{version}");

    public string CreateEmailVerificationToken(Guid userId, string normalizedEmail, DateTimeOffset expiresAt)
    {
        var unixSeconds = expiresAt.ToUnixTimeSeconds();
        var mac = Sign($"email-verify:{userId:N}:{normalizedEmail}:{unixSeconds}");
        return $"{userId:N}.{unixSeconds}.{mac}";
    }

    public bool TryParseEmailVerificationToken(string? token, out Guid userId, out DateTimeOffset expiresAt)
    {
        userId = Guid.Empty;
        expiresAt = default;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Trim().Split('.');
        if (parts.Length != 3 || parts[2].Length != 64)
            return false;

        if (!Guid.TryParseExact(parts[0], "N", out userId) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return false;
        }

        try
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public bool VerifyEmailVerificationToken(string? token, Guid userId, string normalizedEmail, DateTimeOffset utcNow)
    {
        if (!TryParseEmailVerificationToken(token, out var parsedUserId, out var expiresAt) ||
            parsedUserId != userId ||
            expiresAt <= utcNow)
        {
            return false;
        }

        var expected = CreateEmailVerificationToken(userId, normalizedEmail, expiresAt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token!.Trim()));
    }

    private string Sign(string payload)
    {
        var key = options.Value.SigningKey;
        if (key.Length < MinKeyLength)
            throw new InvalidOperationException($"Invite:SigningKey phải có tối thiểu {MinKeyLength} ký tự");

        var data = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), data));
    }
}
