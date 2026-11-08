namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng giao dịch thu / hoàn trong danh sách quản trị (màn O27, O28).
/// </summary>
public class AdminPaymentRow
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string OrderPaymentStatus { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;

    /// <summary>Số tiền lưu trong DB (luôn dương, kể cả khoản hoàn).</summary>
    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Lý do thất bại / từ chối lấy từ hồ sơ hoàn tiền (nếu có).</summary>
    public string? FailureReason { get; set; }

    /// <summary>Mốc giờ của quy tắc chính sách hoàn áp dụng (null nếu không gắn quy tắc).</summary>
    public int? PolicyMinHoursBefore { get; set; }

    /// <summary>Phần trăm hoàn của quy tắc chính sách áp dụng.</summary>
    public decimal? PolicyRefundPercentage { get; set; }

    /// <summary>Khoản hoàn đã có hồ sơ hoàn tiền (xử lý ở /admin/refund-cases).</summary>
    public bool HasRefundCase { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
