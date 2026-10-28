namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ participant invitation.
/// </summary>
public sealed class ParticipantInvitationResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
