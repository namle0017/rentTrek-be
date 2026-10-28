namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Một mốc hoàn tiền: hủy trước ít nhất MinHoursBefore giờ thì được hoàn RefundPercentage phần trăm.
/// </summary>
public class RentalQuoteRefundRuleResponse
{
    public int MinHoursBefore { get; set; }

    public decimal RefundPercentage { get; set; }
}
