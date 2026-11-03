namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể booking participant trong mô hình dữ liệu RenTrek.
/// </summary>
public sealed class BookingParticipant
{
    public Guid ParticipantId { get; set; }
    public Guid BookingOrderId { get; set; }
    public Guid? LinkedUserId { get; set; }
    public Guid? TripGroupId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid TripId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public TripBookingOrder BookingOrder { get; set; } = null!;
    public Account? LinkedUser { get; set; }
    public ICollection<ActivityAttendance> ActivityAttendances { get; set; } = [];
    public ICollection<ParticipantPostRead> ParticipantPostReads { get; set; } = [];
    public Review? Review { get; set; }
    public Trip Trip { get; set; } = null!;
    public TripGroup? TripGroup { get; set; }
}
