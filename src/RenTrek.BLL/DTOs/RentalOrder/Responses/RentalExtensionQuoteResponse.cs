namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Báo giá gia hạn đơn thuê: thời điểm trả cũ / mới, số tiền thêm (không âm) và chi tiết từng dòng. Không ghi dữ liệu.
/// </summary>
public sealed class RentalExtensionQuoteResponse
{
    public Guid OrderId { get; set; }

    public DateTime OldEndAt { get; set; }

    public DateTime NewEndAt { get; set; }

    public int OldBillableDays { get; set; }

    public int NewBillableDays { get; set; }

    /// <summary>Σ(thành tiền mới) − Σ(thành tiền cũ), nhỏ nhất là 0. Khuyến mãi đã áp không tính lại.</summary>
    public decimal ExtraAmount { get; set; }

    public List<RentalExtensionQuoteLineResponse> Lines { get; set; } = [];
}
