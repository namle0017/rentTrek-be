namespace RenTrek.DAL.Entities;

public class RefreshToken
{
    public Guid RefreshTokenId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedByRefreshTokenId { get; set; }

    public Account Account { get; set; } = null!;
    public RefreshToken? ReplacedByRefreshToken { get; set; }
    public ICollection<AccessToken> AccessTokens { get; set; } = new List<AccessToken>();
}
