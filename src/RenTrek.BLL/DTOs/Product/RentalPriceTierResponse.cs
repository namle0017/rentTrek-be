namespace RenTrek.BLL.DTOs.Product;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ rental price tier.
/// </summary>
public class RentalPriceTierResponse
{
    public Guid Id { get; set; }
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
}
