namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Một mốc hoàn tiền: hủy trước ít nhất MinHoursBefore giờ thì được hoàn RefundPercentage phần trăm.
/// </summary>
public class RentalQuoteRefundRuleResponse
{
    public int MinHoursBefore { get; set; }

    public decimal RefundPercentage { get; set; }
}
