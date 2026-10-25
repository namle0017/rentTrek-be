namespace RenTrek.DAL.Entities;

public class Trip
{
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public Guid? RecurrenceRuleId { get; set; }
    public Guid RefundPolicyId { get; set; }
    public Guid ManagedByUserId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSlots { get; set; }
    public string Status { get; set; } = null!;
    public decimal PricePerPerson { get; set; }
    public int MinParticipants { get; set; }
    public int RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<BookingParticipant> BookingParticipants { get; set; } = new List<BookingParticipant>();
    public Account ManagedByUser { get; set; } = null!;
    public TourRecurrenceRule? RecurrenceRule { get; set; }
    public RefundPolicy RefundPolicy { get; set; } = null!;
    public Tour Tour { get; set; } = null!;
    public ICollection<TripBookingOrder> TripBookingOrders { get; set; } = new List<TripBookingOrder>();
    public ICollection<TripGroup> TripGroups { get; set; } = new List<TripGroup>();
    public ICollection<TripProvidedItem> TripProvidedItems { get; set; } = new List<TripProvidedItem>();
}
