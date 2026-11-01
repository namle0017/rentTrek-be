namespace RenTrek.DAL.Models;

/// <summary>
/// Một giao dịch thanh toán / hoàn tiền của đơn.
/// </summary>
public class RentalOrderPaymentRow
{
    public Guid Id { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
