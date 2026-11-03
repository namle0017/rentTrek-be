namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Cảnh báo vận hành khi quy tắc lịch lặp không đủ cấu hình để sinh Trip.
/// </summary>
public sealed class RecurrenceGenerationWarningResponse
{
    public Guid RecurrenceRuleId { get; set; }
    public Guid TourId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
