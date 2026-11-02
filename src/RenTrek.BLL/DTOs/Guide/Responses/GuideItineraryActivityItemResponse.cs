namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Một hoạt động trong timeline lịch trình chuyến đi của Guide tại màn G05 (24.5).
/// </summary>
public sealed class GuideItineraryActivityItemResponse
{
    public Guid ActivityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
    public bool IsCurrent { get; set; }
    public string AttendanceRate { get; set; } = string.Empty;
}
