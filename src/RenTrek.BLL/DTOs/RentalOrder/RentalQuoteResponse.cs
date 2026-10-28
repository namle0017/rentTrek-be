namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Kết quả báo giá đơn thuê. Hệ thống không thu cọc: DepositAmount = 0 và TotalPayable = TotalAmount.
/// </summary>
public class RentalQuoteResponse
{
    public DateTime RentalStartAt { get; set; }

    public DateTime RentalEndAt { get; set; }

    public int BillableDays { get; set; }

    public List<RentalQuoteLineResponse> Lines { get; set; } = [];

    public decimal SubtotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal TotalPayable { get; set; }

    /// <summary>Khuyến mãi áp dụng được; null khi không dùng mã hoặc mã không áp được.</summary>
    public RentalQuotePromotionResponse? Promotion { get; set; }

    /// <summary>Lý do mã khuyến mãi không áp được (báo giá vẫn trả 200).</summary>
    public RentalQuotePromotionErrorResponse? PromotionError { get; set; }

    /// <summary>Chính sách hoàn tiền thuê đang hiệu lực; null nếu chưa cấu hình.</summary>
    public RentalQuoteRefundPolicyResponse? RefundPolicy { get; set; }

    public bool IsAllAvailable { get; set; }

    /// <summary>Số phút giữ chỗ sau khi tạo đơn (rental.holdMinutes).</summary>
    public int HoldMinutes { get; set; }
}
