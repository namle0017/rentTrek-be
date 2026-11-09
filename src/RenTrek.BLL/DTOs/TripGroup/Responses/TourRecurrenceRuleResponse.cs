namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Thông tin quy tắc lịch lặp khởi hành của Tour.
/// </summary>
public sealed class TourRecurrenceRuleResponse
{
    public Guid RecurrenceRuleId { get; set; }
    public Guid TourId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int IntervalValue { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public string? DaysOfWeek { get; set; }
    public int DefaultTotalSlots { get; set; }
    public int? DefaultGroupCount { get; set; }
    public decimal DefaultPrice { get; set; }
    public bool IsActive { get; set; }
}
