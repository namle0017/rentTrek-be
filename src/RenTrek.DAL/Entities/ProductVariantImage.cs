namespace RenTrek.DAL.Entities;

public class ProductVariantImage
{
    public Guid ImageId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;
}
