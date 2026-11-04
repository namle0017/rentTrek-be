namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AvailableAssetResponse
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? ConditionNote { get; set; }
    public string AssetStatus { get; set; } = string.Empty;
}
