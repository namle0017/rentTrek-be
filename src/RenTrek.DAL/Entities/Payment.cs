namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể payment trong mô hình dữ liệu RenTrek.
/// </summary>
public sealed class Payment
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string? TransferCode { get; set; }
    public decimal ReceivedAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public Guid? RefundPolicyRuleId { get; set; }
    public string Purpose { get; set; } = string.Empty;

    public Order Order { get; set; } = null!;
}
