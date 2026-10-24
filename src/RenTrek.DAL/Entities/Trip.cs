namespace RenTrek.DAL.Entities;

public class Trip : BaseEntity
{
    public Guid TourId { get; set; }
    public Tour Tour { get; set; } = null!;

    public Guid? RecurrenceRuleId { get; set; }
    public Guid? RefundPolicyId { get; set; }
    public Guid? ManagedByUserId { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int MinParticipants { get; set; }
    public int BookedSlots { get; set; } = 0;
    public string Status { get; set; } = "Open";
    public decimal PricePerPerson { get; set; }
    public byte[]? RowVersion { get; set; }
}
