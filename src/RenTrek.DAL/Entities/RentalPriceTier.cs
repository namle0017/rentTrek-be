namespace RenTrek.DAL.Entities;

public class RentalPriceTier
{
    public Guid PriceTierId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public Guid? BundleId { get; set; }
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ProductBundle? Bundle { get; set; }
    public ProductVariant? ProductVariant { get; set; }
}
