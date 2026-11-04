namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AssetAssignmentHistoryResponse
{
    public Guid AssignmentId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public DateTime ReservedFrom { get; set; }
    public DateTime ReservedUntil { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AssetRentalChargeResponse
{
    public Guid RentalChargeId { get; set; }
    public Guid RentalOrderId { get; set; }
    public string ChargeType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
