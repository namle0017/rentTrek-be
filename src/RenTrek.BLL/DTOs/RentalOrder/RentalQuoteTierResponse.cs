namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Bậc giá theo số ngày được áp cho dòng thuê.
/// </summary>
public class RentalQuoteTierResponse
{
    public int MinDays { get; set; }

    public int? MaxDays { get; set; }
}
