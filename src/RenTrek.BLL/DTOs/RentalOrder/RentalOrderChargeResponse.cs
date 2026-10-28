namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Khoản phí phát sinh của đơn thuê (không gồm phí nháp của nhân viên).
/// </summary>
public class RentalOrderChargeResponse
{
    public Guid Id { get; set; }

    public string ChargeType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? AssetCode { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
