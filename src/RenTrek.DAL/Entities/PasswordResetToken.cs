namespace RenTrek.DAL.Entities;

public class PasswordResetToken
{
    public Guid PasswordResetTokenId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Account User { get; set; } = null!;
}
