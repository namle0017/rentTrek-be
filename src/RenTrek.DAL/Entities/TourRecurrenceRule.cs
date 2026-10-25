namespace RenTrek.DAL.Entities;

public class TourRecurrenceRule
{
    public Guid RecurrenceRuleId { get; set; }
    public Guid TourId { get; set; }
    public string Frequency { get; set; } = null!;
    public int IntervalValue { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
    public TimeOnly StartTime { get; set; }
    public string? DaysOfWeek { get; set; }
    public int DefaultTotalSlots { get; set; }
    public decimal DefaultPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Tour Tour { get; set; } = null!;
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
