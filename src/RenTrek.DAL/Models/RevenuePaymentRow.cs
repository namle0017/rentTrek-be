namespace RenTrek.DAL.Models;

/// <summary>
/// Một thanh toán thành công để gộp báo cáo doanh thu.
/// </summary>
public class RevenuePaymentRow
{
    public string OrderType { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
