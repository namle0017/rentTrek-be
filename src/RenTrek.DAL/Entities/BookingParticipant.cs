namespace RenTrek.DAL.Entities;

public class BookingParticipant
{
    public Guid ParticipantId { get; set; }
    public Guid BookingOrderId { get; set; }
    public Guid? LinkedUserId { get; set; }
    public Guid? TripGroupId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = null!;
    public Guid TripId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<ActivityAttendance> ActivityAttendances { get; set; } = new List<ActivityAttendance>();
    public TripBookingOrder BookingOrder { get; set; } = null!;
    public Account? LinkedUser { get; set; }
    public ICollection<ParticipantPostRead> ParticipantPostReads { get; set; } = new List<ParticipantPostRead>();
    public Review? Review { get; set; }
    public Trip Trip { get; set; } = null!;
    public TripGroup? TripGroup { get; set; }
}
