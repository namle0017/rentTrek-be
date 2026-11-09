namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalChargeResponse
{
    public Guid ChargeId { get; set; }
    public string ChargeType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AssetCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
}
