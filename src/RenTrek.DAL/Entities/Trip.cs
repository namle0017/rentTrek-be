namespace RenTrek.DAL.Entities;

public sealed class Trip
{
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public Guid? RecurrenceRuleId { get; set; }
    public Guid RefundPolicyId { get; set; }
    public Guid ManagedByUserId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal PricePerPerson { get; set; }
    public int MinParticipants { get; set; }
    public int RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Không có cột trong DB, chỉ phục vụ tour catalog
    public int BookedSlots { get; set; }

    public Tour Tour { get; set; } = null!;
}
