namespace RenTrek.DAL.Entities;

public class Payment
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public string PaymentType { get; set; } = null!;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = null!;
    public string? ProviderTransactionId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? RefundPolicyRuleId { get; set; }
    public string Purpose { get; set; } = null!;

    public ICollection<Payment> InverseOriginalPayment { get; set; } = new List<Payment>();
    public Order Order { get; set; } = null!;
    public Payment? OriginalPayment { get; set; }
    public RefundPolicyRule? RefundPolicyRule { get; set; }
}
