using RenTrek.BLL.DTOs.Product;

namespace RenTrek.BLL.DTOs.ProductBundle;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product bundle (một combo trong danh sách).
/// </summary>
public class ProductBundleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductCategoryResponse Category { get; set; } = new();

    /// <summary>Ví dụ "Túi ngủ ×1 · Đệm hơi ×1".</summary>
    public string ComponentSummary { get; set; } = string.Empty;

    /// <summary>Ảnh chính của thành phần đầu tiên có ảnh.</summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>Giá/ngày thấp nhất trong các bậc giá của combo.</summary>
    public decimal PriceFrom { get; set; }

    /// <summary>null khi không truyền from/to.</summary>
    public BundleAvailabilityResponse? Availability { get; set; }
}
