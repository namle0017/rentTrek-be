namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một biến thể hoặc combo trong bảng giá tổng hợp (§13.13). ItemType: Variant | Bundle.</summary>
public sealed class AdminPricingItemResponse
{
    public string ItemType { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<AdminPriceTierItemResponse> Tiers { get; set; } = [];
}
