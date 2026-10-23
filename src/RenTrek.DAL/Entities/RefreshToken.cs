namespace RenTrek.DAL.Entities;

public class RefreshToken
{
    public long RefreshTokenId { get; set; }
    public long UserId { get; set; }
    public string FamilyId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public long? ReplacedByRefreshTokenId { get; set; }

    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByRefreshToken { get; set; }
    public ICollection<AccessToken> AccessTokens { get; set; } = new List<AccessToken>();
}
