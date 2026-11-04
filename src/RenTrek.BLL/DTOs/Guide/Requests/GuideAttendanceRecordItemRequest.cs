using RenTrek.BLL.Constants;

namespace RenTrek.BLL.DTOs.Guide.Requests;

/// <summary>
/// Bản ghi điểm danh của một khách trong yêu cầu điểm danh hàng loạt của Guide (24.4).
/// </summary>
public sealed class GuideAttendanceRecordItemRequest
{
    public Guid ParticipantId { get; set; }
    public string Status { get; set; } = AttendanceStatuses.Present;
    public string? Reason { get; set; }
}
