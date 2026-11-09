namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AssetResponse
{
    public Guid AssetId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string AssetStatus { get; set; } = string.Empty;
    public string? ConditionNote { get; set; }
    public AssetCurrentAssignmentResponse? CurrentAssignment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
