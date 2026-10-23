namespace RenTrek.BLL.DTOs.Product;

public class ProductDetailResponse : ProductResponse
{
    public string? Description { get; set; }
    public IReadOnlyList<ProductVariantResponse> Variants { get; set; } = [];
}
