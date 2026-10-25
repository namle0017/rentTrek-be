namespace RenTrek.BLL.DTOs.Product;

public class RentalPriceTierResponse
{
    public Guid Id { get; set; }
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
}
