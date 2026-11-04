namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Cột hoạt động trong ma trận điểm danh O14 (11.1).
/// </summary>
public sealed class AttendanceMatrixActivityColumnResponse
{
    public Guid ActivityId { get; set; }
    public int DayNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
}
