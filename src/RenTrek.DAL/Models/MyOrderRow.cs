namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng trong danh sách đơn của khách; chưa ghép tiêu đề (BLL dựng từ FirstItemName / ItemCount / TourName).
/// </summary>
public class MyOrderRow
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Đơn thuê: RentalStartAt; đơn tour: giờ khởi hành của chuyến; còn lại null.</summary>
    public DateTimeOffset? ServiceStartAt { get; set; }

    /// <summary>Đơn thuê: RentalEndAt; đơn tour: giờ kết thúc của chuyến; còn lại null.</summary>
    public DateTimeOffset? ServiceEndAt { get; set; }

    /// <summary>Số dòng thuê chưa xoá (0 với đơn tour).</summary>
    public int ItemCount { get; set; }

    /// <summary>Tên dòng thuê đầu tiên: tên combo, hoặc "Sản phẩm — Biến thể"; null với đơn tour.</summary>
    public string? FirstItemName { get; set; }

    /// <summary>Tên tour của chuyến đã đặt; null với đơn thuê.</summary>
    public string? TourName { get; set; }
}
