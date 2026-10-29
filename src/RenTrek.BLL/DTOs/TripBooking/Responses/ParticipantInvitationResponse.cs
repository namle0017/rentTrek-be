namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>
/// Kết quả tạo lời mời: một mã 6 số chung cho cả nhóm và trạng thái gửi email từng participant.
/// </summary>
public sealed class GroupInvitationResponse
{
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public List<GroupInvitationParticipantResponse> Participants { get; set; } = [];
}

public sealed class GroupInvitationParticipantResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailSent { get; set; }
}
