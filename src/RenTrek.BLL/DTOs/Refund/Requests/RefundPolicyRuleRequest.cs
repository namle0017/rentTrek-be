namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Một bậc hoàn tiền: hủy trước ít nhất MinHoursBefore giờ thì hoàn RefundPercentage %.
/// </summary>
public sealed class RefundPolicyRuleRequest
{
    public int MinHoursBefore { get; set; }
    public decimal RefundPercentage { get; set; }
}
