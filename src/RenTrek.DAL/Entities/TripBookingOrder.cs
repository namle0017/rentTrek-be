namespace RenTrek.DAL.Entities;

public class TripBookingOrder
{
    public Guid OrderId { get; set; }
    public Guid TripId { get; set; }
    public int ParticipantCount { get; set; }
    public decimal UnitPrice { get; set; }
    public string GroupingPreference { get; set; } = null!;
    public bool IsSplit { get; set; }
    public string? SplitReason { get; set; }
    public Guid? SplitApprovedByUserId { get; set; }
    public DateTime? SplitAt { get; set; }

    public ICollection<BookingParticipant> BookingParticipants { get; set; } = new List<BookingParticipant>();
    public Order Order { get; set; } = null!;
    public Account? SplitApprovedByUser { get; set; }
    public Trip Trip { get; set; } = null!;
}
