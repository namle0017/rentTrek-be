namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class GanttVariantGroupResponse
{
    public Guid VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int TotalAssets { get; set; }
    public IReadOnlyList<GanttDailySummaryResponse> DailySummaries { get; set; } = [];
    public IReadOnlyList<GanttAssetItemResponse> Assets { get; set; } = [];
}
