namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu xem trước các chuyến đi sẽ được sinh từ quy tắc lặp (7.5).
/// </summary>
public sealed class PreviewRecurrenceTripsRequest
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}
