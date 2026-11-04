namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Thông tin thành viên trong danh sách nhóm của Guide tại màn G02 (24.2).
/// </summary>
public sealed class GuideGroupParticipantItemResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? Phone { get; set; }
    public string? CurrentAttendanceStatus { get; set; }
}
