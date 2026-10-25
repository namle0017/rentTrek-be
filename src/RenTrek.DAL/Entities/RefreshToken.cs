namespace RenTrek.DAL.Entities;

public class RefreshToken
{
    public Guid RefreshTokenId { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedByRefreshTokenId { get; set; }

    public ICollection<AccessToken> AccessTokens { get; set; } = new List<AccessToken>();
    public RefreshToken? InverseReplacedByRefreshToken { get; set; }
    public RefreshToken? ReplacedByRefreshToken { get; set; }
    public Account User { get; set; } = null!;
}
