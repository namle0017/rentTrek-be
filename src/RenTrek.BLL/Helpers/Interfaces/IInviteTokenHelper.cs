namespace RenTrek.BLL.Helpers.Interfaces;

public interface IInviteTokenHelper
{
    string CreateGroupToken(Guid orderId, int version);
    string CreateEmailVerificationToken(Guid userId, string normalizedEmail, DateTimeOffset expiresAt);
    bool TryParseEmailVerificationToken(string? token, out Guid userId, out DateTimeOffset expiresAt);
    bool VerifyEmailVerificationToken(string? token, Guid userId, string normalizedEmail, DateTimeOffset utcNow);
}
