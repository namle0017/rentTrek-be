namespace RenTrek.BLL.DTOs.ProductBundle.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product bundle component. Quantity là số lượng trên một combo.
/// </summary>
public class ProductBundleComponentResponse
{
    public Guid ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? PrimaryImageUrl { get; set; }

    /// <summary>Số tài sản khả dụng của biến thể trong from-to; null khi không truyền from/to.</summary>
    public int? AvailableQuantity { get; set; }
}
