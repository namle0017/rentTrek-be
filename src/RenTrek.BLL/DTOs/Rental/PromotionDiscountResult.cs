namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Kết quả tính giảm giá: tổng giảm cấp đơn và (nếu phạm vi theo dòng) phần giảm từng dòng.
/// </summary>
public class PromotionDiscountResult
{
    /// <summary>Tổng tiền giảm (VND), luôn trong khoảng [0, tiền gốc].</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Phần giảm từng dòng; rỗng với phạm vi cấp đơn. Tổng Amount bằng DiscountAmount khi phạm vi theo dòng.</summary>
    public List<PromotionLineDiscount> LineDiscounts { get; set; } = [];
}
