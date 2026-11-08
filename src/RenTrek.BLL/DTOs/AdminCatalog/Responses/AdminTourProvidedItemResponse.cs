namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một định mức đồ cấp (§6.18).</summary>
public sealed class AdminTourProvidedItemResponse
{
    public Guid TourProvidedItemId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public string QuantityBasis { get; set; } = string.Empty;
    public string? Note { get; set; }
}
