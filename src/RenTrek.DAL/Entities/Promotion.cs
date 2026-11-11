namespace RenTrek.DAL.Entities;

public class Promotion
{
    public Guid PromotionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = null!;
    public string AppliesTo { get; set; } = null!;
    public string ApplicationScope { get; set; } = null!;
    public string? PromotionCode { get; set; }
    public int? MaxUses { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public bool IsStackable { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Account CreatedByUser { get; set; } = null!;
    public ICollection<PromotionTier> PromotionTiers { get; set; } = new List<PromotionTier>();
}
