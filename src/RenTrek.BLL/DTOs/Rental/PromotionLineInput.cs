namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Dòng thuê (đã tính tiền) làm đầu vào cho việc chia khuyến mãi theo dòng.
/// </summary>
public class PromotionLineInput
{
    public int Index { get; set; }
    public decimal LineAmount { get; set; }
}
