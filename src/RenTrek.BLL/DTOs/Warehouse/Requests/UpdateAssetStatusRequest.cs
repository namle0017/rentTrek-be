namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class UpdateAssetStatusRequest
{
    public string NewStatus { get; set; } = string.Empty;
    public string? AssetStatus { get; set; }
    public string? ConditionNote { get; set; }
}
