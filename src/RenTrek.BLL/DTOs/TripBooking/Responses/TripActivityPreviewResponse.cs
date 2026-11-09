namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>
/// Một hoạt động trong lịch dự kiến của chuyến đi theo giờ Việt Nam.
/// </summary>
public sealed class TripActivityPreviewResponse
{
    public Guid ActivityId { get; set; }
    public int DayNumber { get; set; }
    public string? DayTitle { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset ScheduledStartAt { get; set; }
    public DateTimeOffset ScheduledEndAt { get; set; }
    public bool IsCurrent { get; set; }
}

