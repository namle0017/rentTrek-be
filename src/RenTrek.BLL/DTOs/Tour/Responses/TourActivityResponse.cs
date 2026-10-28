namespace RenTrek.BLL.DTOs.Tour.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ tour activity.
/// </summary>
public class TourActivityResponse
{
    public Guid Id { get; set; }
    public int DayNumber { get; set; }
    public string? DayTitle { get; set; }
    public TimeSpan StartTime { get; set; }
    public string TimeFormatted => StartTime.ToString(@"hh\:mm");
    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
