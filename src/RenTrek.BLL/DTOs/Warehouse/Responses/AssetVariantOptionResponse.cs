namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AssetVariantOptionResponse
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Label => $"{ProductName} — {VariantName} (SKU: {Sku})";
}
