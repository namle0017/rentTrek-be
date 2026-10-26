namespace RenTrek.BLL.DTOs.Product;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product variant.
/// </summary>
public class ProductVariantResponse
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public IReadOnlyList<ProductImageResponse> Images { get; set; } = [];
    public IReadOnlyList<RentalPriceTierResponse> RentalPriceTiers { get; set; } = [];
}
