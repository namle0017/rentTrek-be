namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thay toàn bộ thành phần combo (§13.19).</summary>
public sealed class BundleItemsRequest
{
    public List<BundleItemRequest> Items { get; set; } = [];
}
