namespace RenTrek.DAL.Entities;

public class TourActivity
{
    public Guid TourActivityId { get; set; }
    public Guid TourId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DayNumber { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<ActivityAttendance> ActivityAttendances { get; set; } = new List<ActivityAttendance>();
    public Tour Tour { get; set; } = null!;
}
