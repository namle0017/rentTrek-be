namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Một bậc giá thuê theo số ngày (MaxDays null nghĩa là không giới hạn trên).
/// </summary>
public class RentalPriceTierOption
{
    public Guid PriceTierId { get; set; }
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
}
