using RenTrek.DAL.Entities.Enums;

namespace RenTrek.DAL.Entities;

public class ProductVariant
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public decimal? SalePrice { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Product Product { get; set; } = null!;
    public ICollection<ProductVariantImage> Images { get; set; } = new List<ProductVariantImage>();
    public ICollection<RentalPriceTier> RentalPriceTiers { get; set; } = new List<RentalPriceTier>();
}
