namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>Quyết toán cọc hiển thị cho khách (contract 03.1 §5.4, PRD §8.7). Chỉ là số tính toán.</summary>
public class RentalOrderDepositSettlementResponse
{
    public decimal DepositHeld { get; set; }
    public decimal ValidCharges { get; set; }
    public decimal DepositToRefund { get; set; }
    public decimal AdditionalAmountDue { get; set; }
}
