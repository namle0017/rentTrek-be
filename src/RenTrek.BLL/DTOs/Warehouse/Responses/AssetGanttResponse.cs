namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AssetGanttResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public IReadOnlyList<GanttVariantGroupResponse> Variants { get; set; } = [];
}

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

public class GanttDailySummaryResponse
{
    public string Date { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int InUseCount { get; set; }
    public int AvailableCount { get; set; }
    public bool IsOverCapacity { get; set; }
}

public class GanttAssetItemResponse
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetStatus { get; set; } = string.Empty;
    public IReadOnlyList<GanttTimelineBlockResponse> Timeline { get; set; } = [];
}

public class GanttTimelineBlockResponse
{
    public Guid AssignmentId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
}
