namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AssetGanttResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public IReadOnlyList<GanttVariantGroupResponse> Variants { get; set; } = [];
}
