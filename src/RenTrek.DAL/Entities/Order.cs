namespace RenTrek.DAL.Entities;

public class Order
{
    public Guid OrderId { get; set; }
    public Guid CustomerUserId { get; set; }
    public string OrderType { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string OrderCode { get; set; } = null!;
    public string? Note { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string PaymentStatus { get; set; } = null!;
    public DateTime? HoldExpiresAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public Guid RefundPolicyId { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<AppliedPromotion> AppliedPromotions { get; set; } = new List<AppliedPromotion>();
    public Account CustomerUser { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public RefundPolicy RefundPolicy { get; set; } = null!;
    public RentalOrder? RentalOrder { get; set; }
    public TripBookingOrder? TripBookingOrder { get; set; }
}
