namespace RenTrek.DAL.Entities;

public class ProductBundle
{
    public Guid BundleId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<ProductBundleItem> ProductBundleItems { get; set; } = new List<ProductBundleItem>();
    public ICollection<RentalOrderItem> RentalOrderItems { get; set; } = new List<RentalOrderItem>();
    public ICollection<RentalPriceTier> RentalPriceTiers { get; set; } = new List<RentalPriceTier>();
}
