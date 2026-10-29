namespace RenTrek.BLL.DTOs.Payment.Responses;

/// <summary>
/// Kết quả khởi tạo thanh toán (contract 03.1 §6.1).
/// </summary>
public sealed class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
    public BankTransferResponse BankTransfer { get; set; } = new();
}
