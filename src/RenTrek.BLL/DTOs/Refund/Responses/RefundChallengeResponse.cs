namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Challenge OTP vừa gửi; không bao giờ chứa mã.
/// </summary>
public sealed class RefundChallengeResponse
{
    public Guid ChallengeId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
