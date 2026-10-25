namespace RenTrek.BLL.DTOs.Product;

public class ProductVariantResponse
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public decimal? SalePrice { get; set; }
    public IReadOnlyList<ProductImageResponse> Images { get; set; } = [];
    public IReadOnlyList<RentalPriceTierResponse> RentalPriceTiers { get; set; } = [];
}
