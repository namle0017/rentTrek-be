namespace RenTrek.BLL.DTOs.Order;

/// <summary>
/// Mô tả dữ liệu trả về của một dòng trong danh sách đơn hàng của tôi (đơn thuê hoặc đơn đặt chuyến).
/// </summary>
public class MyOrderResponse
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    /// <summary>Số tiền khách phải trả; hiện bằng TotalAmount (không có cọc).</summary>
    public decimal TotalPayable { get; set; }

    public DateTimeOffset? ServiceStartAt { get; set; }
    public DateTimeOffset? ServiceEndAt { get; set; }

    /// <summary>Đơn thuê: tên dòng đầu tiên (kèm " +N" khi có nhiều dòng); đơn đặt chuyến: tên tour.</summary>
    public string Title { get; set; } = string.Empty;

    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
