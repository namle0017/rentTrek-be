namespace RenTrek.DAL.Entities;

public class ActivityAttendance
{
    public Guid AttendanceId { get; set; }
    public Guid TourActivityId { get; set; }
    public Guid ParticipantId { get; set; }
    public string AttendanceStatus { get; set; } = null!;
    public DateTime CheckedAt { get; set; }
    public Guid CheckedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Account CheckedByUser { get; set; } = null!;
    public BookingParticipant Participant { get; set; } = null!;
    public TourActivity TourActivity { get; set; } = null!;
}
