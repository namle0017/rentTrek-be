namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Một bậc hoàn tiền. Description sinh từ số giờ và phần trăm (DB không có cột mô tả).
/// </summary>
public sealed class RefundPolicyRuleResponse
{
    public Guid RuleId { get; set; }
    public int MinHoursBefore { get; set; }
    public decimal RefundPercentage { get; set; }
    public string Description { get; set; } = string.Empty;
}
