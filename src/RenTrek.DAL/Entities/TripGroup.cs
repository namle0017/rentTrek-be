namespace RenTrek.DAL.Entities;

public class TripGroup
{
    public Guid TripGroupId { get; set; }
    public Guid TripId { get; set; }
    public Guid GuideUserId { get; set; }
    public string GroupName { get; set; } = null!;
    public int MaxParticipants { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<BookingParticipant> BookingParticipants { get; set; } = new List<BookingParticipant>();
    public Account GuideUser { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
}
