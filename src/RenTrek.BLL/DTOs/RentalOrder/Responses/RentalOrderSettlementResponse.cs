namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>Quyết toán cọc đã chốt.</summary>
public class RentalOrderSettlementResponse
{
    public decimal RentalFeePaid { get; set; }
    public decimal DepositHeld { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal RentalFeeRefund { get; set; }
    public decimal DepositRefund { get; set; }
    public decimal AmountOwed { get; set; }
    public DateTime SettledAt { get; set; }
}
