namespace RenTrek.BLL.DTOs.Promotion.Internal;

/// <summary>
/// Số tiền giảm áp cho một dòng thuê.
/// </summary>
public class PromotionLineDiscountItem
{
    public int LineIndex { get; set; }
    public decimal Amount { get; set; }
}
