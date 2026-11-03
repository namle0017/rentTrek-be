namespace RenTrek.DAL.Entities;

/// <summary>
/// Mã OTP một lần cho thao tác xuất danh sách hoàn tiền của Admin.
/// </summary>
public sealed class RefundActionChallenge
{
    public Guid ChallengeId { get; set; }
    public Guid AdminUserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
