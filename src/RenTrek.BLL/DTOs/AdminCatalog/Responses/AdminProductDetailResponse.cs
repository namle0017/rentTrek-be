namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>
/// Chi tiết sản phẩm cho vận hành: gồm cả sản phẩm / biến thể tạm ngưng và số thiết bị.
/// </summary>
public sealed class AdminProductDetailResponse
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<AdminProductVariantResponse> Variants { get; set; } = [];
}

public sealed class AdminProductVariantResponse
{
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<string> ImageUrls { get; set; } = [];
    public IReadOnlyList<AdminPriceTierResponse> PriceTiers { get; set; } = [];
    public int TotalAssetsCount { get; set; }
    public int AvailableAssetsCount { get; set; }
}

public sealed record AdminPriceTierResponse(int MinDays, int? MaxDays, decimal DailyPrice);
