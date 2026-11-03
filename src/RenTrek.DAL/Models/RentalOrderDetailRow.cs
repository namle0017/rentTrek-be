namespace RenTrek.DAL.Models;

/// <summary>
/// Toàn bộ dữ liệu chi tiết đơn thuê của khách, đọc bằng vài truy vấn chiếu (không N+1).
/// </summary>
public class RentalOrderDetailRow
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public decimal DepositAmount { get; set; }

    // Liên hệ khách (chỉ dùng ở màn vận hành)
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }

    /// <summary>Họ tên nhân viên xử lý; null nếu chưa có.</summary>
    public string? HandledByName { get; set; }

    public Guid RefundPolicyId { get; set; }
    public string RefundPolicyName { get; set; } = string.Empty;
    public int RefundPolicyVersion { get; set; }

    public List<RentalOrderLineRow> Lines { get; set; } = [];
    public List<RentalOrderAssignmentRow> Assignments { get; set; } = [];
    public List<RentalOrderChargeRow> Charges { get; set; } = [];
    public List<RentalOrderPaymentRow> Payments { get; set; } = [];
}
