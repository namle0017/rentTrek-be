namespace RenTrek.DAL.Entities;

public class PromotionTier
{
    public Guid PromotionTierId { get; set; }
    public Guid PromotionId { get; set; }
    public Guid? GiftProductVariantId { get; set; }
    public decimal MinThreshold { get; set; }
    public string DiscountType { get; set; } = null!;
    public decimal DiscountValue { get; set; }
    public int? GiftQuantity { get; set; }
    public string QualificationMetric { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<AppliedPromotion> AppliedPromotions { get; set; } = new List<AppliedPromotion>();
    public ProductVariant? GiftProductVariant { get; set; }
    public Promotion Promotion { get; set; } = null!;
}
