namespace RenTrek.BLL.DTOs.RentalOps.Responses;

/// <summary>Quyết toán cọc (PRD §8.7). Chỉ là số tính toán, chưa chuyển tiền.</summary>
public sealed class AdminRentalSettlementResponse
{
    public decimal DepositHeld { get; set; }
    public decimal ValidCharges { get; set; }
    public decimal DepositToRefund { get; set; }
    public decimal AdditionalAmountDue { get; set; }
}
