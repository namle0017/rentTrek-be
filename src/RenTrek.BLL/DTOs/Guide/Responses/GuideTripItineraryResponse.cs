namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Lịch trình chuyến đi và tiến độ hoạt động của Guide tại màn G05 (24.5).
/// </summary>
public sealed class GuideTripItineraryResponse
{
    public Guid TripId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public IReadOnlyList<GuideItineraryDayItemResponse> Days { get; set; } = [];
}
