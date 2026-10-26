namespace RenTrek.BLL.DTOs.Product;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product detail.
/// </summary>
public class ProductDetailResponse : ProductResponse
{
    public string? Description { get; set; }
    public IReadOnlyList<ProductVariantResponse> Variants { get; set; } = [];
}
