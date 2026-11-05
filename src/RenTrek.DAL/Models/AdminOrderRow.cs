namespace RenTrek.DAL.Models;

/// <summary>
/// Một đơn trong danh sách / đầu trang chi tiết đơn quản trị.
/// </summary>
public class AdminOrderRow
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public Guid CustomerUserId { get; set; }
    public string? CustomerUserCode { get; set; }
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }

    /// <summary>Tên tour (đơn đặt chuyến); null với đơn thuê.</summary>
    public string? TourName { get; set; }

    /// <summary>Số hành khách (đơn đặt chuyến).</summary>
    public int ParticipantCount { get; set; }

    /// <summary>Tổng số món chưa xoá (đơn thuê).</summary>
    public int RentalItemQuantity { get; set; }

    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset? ReconciliationDeadlineAt { get; set; }

    /// <summary>Có khoản hoàn tiền đang chờ duyệt / chờ xử lý.</summary>
    public bool HasPendingRefund { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
