namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Giá trị ô điểm danh của một khách tại một hoạt động trong ma trận O14 (11.1).
/// </summary>
public sealed class AttendanceCellValueResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; }
    public string? CheckedBy { get; set; }
}
