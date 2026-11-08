namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một bậc giá thuê kèm id (§13.11, §13.20).</summary>
public sealed class AdminPriceTierItemResponse
{
    public Guid PriceTierId { get; set; }
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
}
