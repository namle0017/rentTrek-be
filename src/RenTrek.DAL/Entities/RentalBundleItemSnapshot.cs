namespace RenTrek.DAL.Entities;

public class RentalBundleItemSnapshot
{
    public Guid RentalBundleItemSnapshotId { get; set; }
    public Guid RentalOrderItemId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int QuantityPerBundle { get; set; }
    public DateTime CreatedAt { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;
    public RentalOrderItem RentalOrderItem { get; set; } = null!;
}
