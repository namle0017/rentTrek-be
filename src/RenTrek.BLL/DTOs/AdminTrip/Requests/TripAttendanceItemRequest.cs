namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Một bản ghi điểm danh cá nhân trong yêu cầu cập nhật điểm danh hoạt động (11.2).
/// </summary>
public sealed class TripAttendanceItemRequest
{
    public Guid ParticipantId { get; set; }
    public string Status { get; set; } = "Present";
    public string? Reason { get; set; }
}
