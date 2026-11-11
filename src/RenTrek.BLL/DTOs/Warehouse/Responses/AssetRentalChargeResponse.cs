namespace RenTrek.BLL.DTOs.Warehouse.Responses;

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
