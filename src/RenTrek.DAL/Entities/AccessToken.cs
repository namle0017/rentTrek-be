namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể access token trong mô hình dữ liệu RenTrek.
/// </summary>
public class AccessToken
{
    public Guid AccessTokenId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid RefreshTokenId { get; set; }
    public string Jti { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account Account { get; set; } = null!;
    public RefreshToken RefreshToken { get; set; } = null!;
}
