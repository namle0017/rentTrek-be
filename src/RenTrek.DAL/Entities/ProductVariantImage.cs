namespace RenTrek.DAL.Entities;

public class ProductVariantImage
{
    public long Id { get; set; }
    public long ProductVariantId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
}
