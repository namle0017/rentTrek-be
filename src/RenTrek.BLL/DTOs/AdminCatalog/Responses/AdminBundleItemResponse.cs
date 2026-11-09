namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một thành phần combo kèm thông tin biến thể; IsActive = biến thể và sản phẩm còn bán.</summary>
public sealed class AdminBundleItemResponse
{
    public Guid BundleItemId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsActive { get; set; }
}
