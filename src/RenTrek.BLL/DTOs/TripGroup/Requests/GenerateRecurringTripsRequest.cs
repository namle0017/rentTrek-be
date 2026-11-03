namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Yêu cầu sinh Trip và TripGroup từ các quy tắc lịch lặp đang hoạt động.
/// </summary>
public sealed class GenerateRecurringTripsRequest
{
    public Guid? TourId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}
