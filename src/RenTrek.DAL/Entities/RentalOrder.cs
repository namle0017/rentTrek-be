namespace RenTrek.DAL.Entities;

public class RentalOrder
{
    public Guid OrderId { get; set; }
    public Guid? HandledByUserId { get; set; }
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public decimal DepositAmount { get; set; }
    public Guid? RentalPolicyId { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? CustomerReceivedConfirmedAt { get; set; }
    public string? ReceiptConfirmSource { get; set; }
    public string? CancellationType { get; set; }

    public Account? HandledByUser { get; set; }
    public Order Order { get; set; } = null!;
    public ICollection<RentalCharge> RentalCharges { get; set; } = new List<RentalCharge>();
    public ICollection<RentalOrderItem> RentalOrderItems { get; set; } = new List<RentalOrderItem>();
    public RentalPolicy? RentalPolicy { get; set; }
    public RentalSettlement? Settlement { get; set; }
}
