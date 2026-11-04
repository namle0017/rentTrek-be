namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Kết quả sinh hàng loạt chuyến đi từ quy tắc lặp (7.6).
/// </summary>
public sealed class GenerateRecurrenceTripsResponse
{
    public Guid RuleId { get; set; }
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public IReadOnlyList<Guid> CreatedTripIds { get; set; } = [];
}
