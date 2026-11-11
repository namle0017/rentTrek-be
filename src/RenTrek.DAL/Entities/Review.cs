namespace RenTrek.DAL.Entities;

public class Review
{
    public Guid ReviewId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ParticipantId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }

    public BookingParticipant Participant { get; set; } = null!;
}
