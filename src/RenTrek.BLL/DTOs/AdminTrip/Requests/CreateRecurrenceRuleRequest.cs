namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu tạo quy tắc lặp chuyến mới cho tour (7.2).
/// </summary>
public sealed class CreateRecurrenceRuleRequest
{
    public string Frequency { get; set; } = "Weekly";
    public int IntervalValue { get; set; } = 1;
    public string? DaysOfWeek { get; set; }
    public string StartTime { get; set; } = "06:00:00";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int DefaultTotalSlots { get; set; } = 20;
    public decimal? DefaultPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
