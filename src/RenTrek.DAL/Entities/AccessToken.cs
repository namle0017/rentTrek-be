namespace RenTrek.DAL.Entities;

public class AccessToken
{
    public long AccessTokenId { get; set; }
    public long UserId { get; set; }
    public long RefreshTokenId { get; set; }
    public string Jti { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public RefreshToken RefreshToken { get; set; } = null!;
}
