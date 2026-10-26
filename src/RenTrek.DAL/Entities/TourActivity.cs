namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể tour activity trong mô hình dữ liệu RenTrek.
/// </summary>
public class TourActivity : BaseEntity
{
    public Guid TourId { get; set; }
    public Tour Tour { get; set; } = null!;

    public int DayNumber { get; set; }
    public string? DayTitle { get; set; } // Ví dụ: Trạm Tôn → Trại 2.800m
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<ActivityAttendance> ActivityAttendances { get; set; } = [];
}
