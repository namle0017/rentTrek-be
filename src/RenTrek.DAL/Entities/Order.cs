namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể order trong mô hình dữ liệu RenTrek.
/// </summary>
public sealed class Order
{
    public Guid OrderId { get; set; }
    public Guid CustomerUserId { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public Guid RefundPolicyId { get; set; }
    public bool IsDeleted { get; set; }
    public string? IdempotencyKey { get; set; }

    public TripBookingOrder TripBookingOrder { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
