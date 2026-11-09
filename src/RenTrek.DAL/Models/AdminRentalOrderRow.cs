namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng danh sách đơn thuê cho vận hành, kèm số thiết bị theo trạng thái giao nhận.
/// </summary>
public class AdminRentalOrderRow
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Gán thiết bị chưa huỷ.</summary>
    public int AssetsCount { get; set; }
    public int IssuedCount { get; set; }

    /// <summary>Đã trả hoặc mất / sự cố.</summary>
    public int ReturnedCount { get; set; }
    public int DraftChargesCount { get; set; }
    public bool IsOverdue { get; set; }
}
