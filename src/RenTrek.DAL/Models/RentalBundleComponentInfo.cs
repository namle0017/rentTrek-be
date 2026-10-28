namespace RenTrek.DAL.Models;

/// <summary>
/// Thành phần của combo thuê: biến thể và số lượng cho mỗi combo.
/// </summary>
public class RentalBundleComponentInfo
{
    public Guid VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityPerBundle { get; set; }

    /// <summary>True khi biến thể và sản phẩm của thành phần đều Active và chưa bị xoá.</summary>
    public bool IsActive { get; set; }
}
