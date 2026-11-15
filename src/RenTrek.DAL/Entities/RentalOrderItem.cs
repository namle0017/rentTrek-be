namespace RenTrek.DAL.Entities;

public class RentalOrderItem
{
    public Guid RentalOrderItemId { get; set; }
    public Guid RentalOrderId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal DailyPrice { get; set; }
    public decimal LineAmount { get; set; }
    public Guid? BundleId { get; set; }
    public int BillableDays { get; set; }
    public decimal UnitReplacementValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<AppliedPromotion> AppliedPromotions { get; set; } = new List<AppliedPromotion>();
    public ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();
    public ProductBundle? Bundle { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public ICollection<RentalBundleItemSnapshot> RentalBundleItemSnapshots { get; set; } = new List<RentalBundleItemSnapshot>();
    public RentalOrder RentalOrder { get; set; } = null!;
}
