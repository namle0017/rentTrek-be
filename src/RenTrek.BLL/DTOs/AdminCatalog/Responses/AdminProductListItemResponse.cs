namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

public sealed class AdminProductListItemResponse
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public int VariantsCount { get; set; }
    public int TotalAssetsCount { get; set; }
    public int AvailableAssetsCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
