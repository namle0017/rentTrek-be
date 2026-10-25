using RenTrek.DAL.Entities.Enums;

namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể product variant trong mô hình dữ liệu RenTrek.
/// </summary>
public class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Product Product { get; set; } = null!;
    public ICollection<ProductVariantImage> Images { get; set; } = new List<ProductVariantImage>();
    public ICollection<RentalPriceTier> RentalPriceTiers { get; set; } = new List<RentalPriceTier>();
}
