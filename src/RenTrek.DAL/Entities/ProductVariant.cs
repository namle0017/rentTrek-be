namespace RenTrek.DAL.Entities;

public class ProductVariant
{
    public Guid ProductVariantId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = null!;
    public string VariantName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<AppliedPromotion> AppliedPromotions { get; set; } = new List<AppliedPromotion>();
    public ICollection<EquipmentAsset> EquipmentAssets { get; set; } = new List<EquipmentAsset>();
    public Product Product { get; set; } = null!;
    public ICollection<ProductBundleItem> ProductBundleItems { get; set; } = new List<ProductBundleItem>();
    public ICollection<ProductVariantImage> ProductVariantImages { get; set; } = new List<ProductVariantImage>();
    public ICollection<PromotionTier> PromotionTiers { get; set; } = new List<PromotionTier>();
    public ICollection<RentalBundleItemSnapshot> RentalBundleItemSnapshots { get; set; } = new List<RentalBundleItemSnapshot>();
    public ICollection<RentalOrderItem> RentalOrderItems { get; set; } = new List<RentalOrderItem>();
    public ICollection<RentalPriceTier> RentalPriceTiers { get; set; } = new List<RentalPriceTier>();
    public ICollection<TourProvidedItem> TourProvidedItems { get; set; } = new List<TourProvidedItem>();
    public ICollection<TripProvidedItem> TripProvidedItems { get; set; } = new List<TripProvidedItem>();
}
