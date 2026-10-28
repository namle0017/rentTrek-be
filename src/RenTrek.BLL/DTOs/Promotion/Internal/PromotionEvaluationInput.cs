namespace RenTrek.BLL.DTOs.Promotion.Internal;

/// <summary>
/// Dữ liệu đầu vào để đánh giá một mã khuyến mãi cho đơn hàng (số tiền đã được server tính sẵn).
/// </summary>
public class PromotionEvaluationInput
{
    /// <summary>Mã khuyến mãi khách nhập; để trống nghĩa là không dùng khuyến mãi.</summary>
    public string? PromotionCode { get; set; }

    /// <summary>Loại đơn cần áp mã (xem OrderTypes).</summary>
    public string OrderType { get; set; } = string.Empty;

    /// <summary>Tổng tiền hàng trước giảm.</summary>
    public decimal SubtotalAmount { get; set; }

    /// <summary>Số tiền của từng dòng, dùng khi mã áp dụng theo dòng.</summary>
    public List<PromotionLineInput> Lines { get; set; } = [];

    /// <summary>Tổng số lượng của mọi dòng (điều kiện bậc theo số lượng).</summary>
    public int TotalQuantity { get; set; }

    /// <summary>Số ngày tính tiền của đơn thuê (điều kiện bậc theo số ngày).</summary>
    public int BillableDays { get; set; }

    /// <summary>Khách đặt đơn, dùng để đếm lượt dùng tối đa theo người.</summary>
    public Guid CustomerUserId { get; set; }

    /// <summary>Thời điểm đánh giá (UTC).</summary>
    public DateTime NowUtc { get; set; }
}
