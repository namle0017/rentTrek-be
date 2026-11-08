namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một hoạt động lịch trình (§6.11).</summary>
public sealed class AdminTourActivityResponse
{
    public Guid ActivityId { get; set; }
    public int DayNumber { get; set; }
    public string? DayTitle { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}
