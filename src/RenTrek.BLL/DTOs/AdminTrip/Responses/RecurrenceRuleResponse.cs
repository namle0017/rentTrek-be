namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin quy tắc lặp chuyến đi của tour (7.1, 7.2, 7.3).
/// </summary>
public sealed class RecurrenceRuleResponse
{
    public Guid RecurrenceRuleId { get; set; }
    public Guid TourId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int IntervalValue { get; set; }
    public string? DaysOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int DefaultTotalSlots { get; set; }
    public decimal? DefaultPrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
