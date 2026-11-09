namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thêm / sửa một hoạt động lịch trình (§6.12, §6.13).</summary>
public sealed class SaveTourActivityRequest
{
    public int DayNumber { get; set; }
    public string? DayTitle { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
}
