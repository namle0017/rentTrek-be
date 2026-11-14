namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

public sealed class AdminProductVariantResponse
{
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? ReplacementValue { get; set; }
    public IReadOnlyList<string> ImageUrls { get; set; } = [];
    /// <summary>Ảnh kèm id để xoá (§13.10); cùng thứ tự với ImageUrls.</summary>
    public IReadOnlyList<AdminVariantImageResponse> Images { get; set; } = [];
    public IReadOnlyList<AdminPriceTierResponse> PriceTiers { get; set; } = [];
    public int TotalAssetsCount { get; set; }
    public int AvailableAssetsCount { get; set; }
}
