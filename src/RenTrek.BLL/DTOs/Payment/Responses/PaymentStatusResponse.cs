namespace RenTrek.BLL.DTOs.Payment.Responses;

/// <summary>
/// Trạng thái một lần thanh toán để FE poll (contract 03.1 §6.2).
/// </summary>
public sealed class PaymentStatusResponse
{
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string ReconciliationStatus { get; set; } = "None";
    public string? RefundStatus { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string OrderPaymentStatus { get; set; } = string.Empty;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? FailureReason { get; set; }
}
