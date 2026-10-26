namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể product variant image trong mô hình dữ liệu RenTrek.
/// </summary>
public class ProductVariantImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductVariantId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
}
