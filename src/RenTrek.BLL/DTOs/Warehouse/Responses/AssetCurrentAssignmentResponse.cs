namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AssetCurrentAssignmentResponse
{
    public Guid AssignmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public DateTime ReservedFrom { get; set; }
    public DateTime ReservedUntil { get; set; }
}
