namespace RenTrek.DAL.Models;

/// <summary>
/// Thành phần của combo kèm ảnh chính của biến thể, dùng cho danh sách và chi tiết combo.
/// </summary>
public class ProductBundleComponentRow
{
    public Guid BundleId { get; set; }
    public Guid VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>True khi biến thể và sản phẩm của thành phần đều Active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Ảnh chính (IsPrimary, rồi DisplayOrder) chưa xoá của biến thể; null nếu chưa có ảnh.</summary>
    public string? PrimaryImageUrl { get; set; }
}
