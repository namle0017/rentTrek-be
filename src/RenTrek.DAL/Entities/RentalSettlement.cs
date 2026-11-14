namespace RenTrek.DAL.Entities;

/// <summary>
/// Kết quả quyết toán cọc của một đơn thuê (1-1 với rental_orders).
/// </summary>
public class RentalSettlement
{
    public Guid RentalOrderId { get; set; }
    public decimal RentalFeePaid { get; set; }
    public decimal DepositHeld { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal RentalFeeRefund { get; set; }
    public decimal DepositRefund { get; set; }
    public decimal AmountOwed { get; set; }
    public Guid? RefundCaseId { get; set; }
    public Guid? ExtraPaymentId { get; set; }
    public Guid? SettledByUserId { get; set; }
    public DateTime SettledAt { get; set; }

    public RentalOrder RentalOrder { get; set; } = null!;
    public RefundCase? RefundCase { get; set; }
    public Payment? ExtraPayment { get; set; }
}
