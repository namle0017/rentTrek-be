namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class GanttAssetItemResponse
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetStatus { get; set; } = string.Empty;
    public IReadOnlyList<GanttTimelineBlockResponse> Timeline { get; set; } = [];
}
