using RenTrek.BLL.DTOs.Product;

namespace RenTrek.BLL.DTOs.ProductBundle;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product bundle detail.
/// </summary>
public class ProductBundleDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ProductBundleComponentResponse> Components { get; set; } = [];
    public List<RentalPriceTierResponse> PriceTiers { get; set; } = [];

    /// <summary>null khi không truyền from/to.</summary>
    public BundleAvailabilityResponse? Availability { get; set; }
}
