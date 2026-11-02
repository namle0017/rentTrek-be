namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Lịch sử điểm danh qua các chặng của một khách tại màn G03 (24.3).
/// </summary>
public sealed class GuideParticipantAttendanceItemResponse
{
    public Guid ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public int DayNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; }
}
