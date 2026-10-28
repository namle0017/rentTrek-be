namespace RenTrek.DAL.Models;

/// <summary>
/// Khoản phí phát sinh của đơn thuê (BLL tự lọc trạng thái Draft khi trả cho khách).
/// </summary>
public class RentalOrderChargeRow
{
    public Guid Id { get; set; }
    public string ChargeType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AssetCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
}
