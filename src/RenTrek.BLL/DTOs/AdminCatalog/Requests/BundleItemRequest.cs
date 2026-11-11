namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Một thành phần combo: biến thể và số lượng.</summary>
public sealed class BundleItemRequest
{
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }
}
