namespace RenTrek.DAL.Entities;

public class AccessToken
{
    public Guid AccessTokenId { get; set; }
    public Guid UserId { get; set; }
    public Guid RefreshTokenId { get; set; }
    public string Jti { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public RefreshToken RefreshToken { get; set; } = null!;
    public Account User { get; set; } = null!;
}
