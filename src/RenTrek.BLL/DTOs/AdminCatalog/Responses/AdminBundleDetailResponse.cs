namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Chi tiết combo: thông tin, thành phần và bậc giá (§13.16).</summary>
public sealed class AdminBundleDetailResponse
{
    public Guid BundleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? ReplacementValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AdminBundleItemResponse> Items { get; set; } = [];
    public List<AdminPriceTierItemResponse> PriceTiers { get; set; } = [];
}
