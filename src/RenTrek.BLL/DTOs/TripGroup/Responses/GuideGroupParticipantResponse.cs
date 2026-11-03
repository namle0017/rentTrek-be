namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Thông tin người tham gia trong group dành cho hướng dẫn viên (chỉ những người thuộc đơn Paid đang hiệu lực).
/// </summary>
public sealed class GuideGroupParticipantResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string Status { get; set; } = string.Empty;
}
