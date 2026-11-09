namespace RenTrek.DAL.Entities;

public class ProductBundleItem
{
    public Guid BundleItemId { get; set; }
    public Guid BundleId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ProductBundle Bundle { get; set; } = null!;
    public ProductVariant ProductVariant { get; set; } = null!;
}
