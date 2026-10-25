namespace RenTrek.DAL.Entities;

public class ParticipantPostRead
{
    public Guid ParticipantPostReadId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid PostId { get; set; }
    public DateTime ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public BookingParticipant Participant { get; set; } = null!;
    public Post Post { get; set; } = null!;
}
