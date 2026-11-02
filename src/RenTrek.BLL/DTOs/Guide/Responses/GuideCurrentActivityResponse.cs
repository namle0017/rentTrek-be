namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Hoạt động đang diễn ra hiện tại của nhóm tại màn G02 (24.2).
/// </summary>
public sealed class GuideCurrentActivityResponse
{
    public Guid ActivityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DayNumber { get; set; }
    public string StartTime { get; set; } = string.Empty;
}
