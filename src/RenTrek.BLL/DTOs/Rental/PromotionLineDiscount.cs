namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Số tiền giảm áp cho một dòng thuê.
/// </summary>
public class PromotionLineDiscount
{
    public int LineIndex { get; set; }
    public decimal Amount { get; set; }
}
