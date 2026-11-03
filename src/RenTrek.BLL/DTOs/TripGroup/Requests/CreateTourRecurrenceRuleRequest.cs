namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Yêu cầu tạo quy tắc lịch lặp khởi hành cho Tour.
/// </summary>
public sealed class CreateTourRecurrenceRuleRequest
{
    public string Frequency { get; set; } = "Weekly";
    public int IntervalValue { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public string? DaysOfWeek { get; set; }
    public int DefaultTotalSlots { get; set; }
    public int DefaultGroupCount { get; set; }
    public decimal DefaultPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
