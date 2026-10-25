namespace RenTrek.DAL.Entities;

public class AppliedPromotion
{
    public Guid AppliedPromotionId { get; set; }
    public Guid PromotionTierId { get; set; }
    public decimal DiscountAmount { get; set; }
    public int? GiftQuantity { get; set; }
    public DateTime AppliedAt { get; set; }
    public Guid OrderId { get; set; }
    public Guid? RentalOrderItemId { get; set; }
    public Guid? GiftProductVariantId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();
    public ProductVariant? GiftProductVariant { get; set; }
    public Order Order { get; set; } = null!;
    public PromotionTier PromotionTier { get; set; } = null!;
    public RentalOrderItem? RentalOrderItem { get; set; }
}
