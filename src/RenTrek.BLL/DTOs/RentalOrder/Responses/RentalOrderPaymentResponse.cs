namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Giao dịch thanh toán hoặc hoàn tiền của đơn thuê.
/// </summary>
public class RentalOrderPaymentResponse
{
    public Guid Id { get; set; }

    public string PaymentType { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public decimal ReceivedAmount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
