namespace RenTrek.DAL.Entities;

public class RentalPriceTier
{
    public long Id { get; set; }
    public long ProductVariantId { get; set; }
    public int MinDays { get; set; } = 1;
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
}
