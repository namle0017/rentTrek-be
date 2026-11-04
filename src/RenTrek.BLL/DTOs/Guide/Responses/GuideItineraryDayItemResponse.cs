namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Một ngày trong timeline lịch trình chuyến đi của Guide tại màn G05 (24.5).
/// </summary>
public sealed class GuideItineraryDayItemResponse
{
    public int DayNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<GuideItineraryActivityItemResponse> Activities { get; set; } = [];
}
