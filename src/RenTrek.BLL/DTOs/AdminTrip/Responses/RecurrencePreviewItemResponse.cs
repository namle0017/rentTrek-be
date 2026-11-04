namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Một mục xem trước chuyến đi sẽ được sinh từ quy tắc lặp (7.5).
/// </summary>
public sealed class RecurrencePreviewItemResponse
{
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public bool IsAlreadyGenerated { get; set; }
    public Guid? ExistingTripId { get; set; }
}
