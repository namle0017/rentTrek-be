namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class UpdateAssetStatusRequest
{
    public string NewStatus { get; set; } = string.Empty;
    public string ConditionNote { get; set; } = string.Empty;
}
