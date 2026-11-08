namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Body thay toàn bộ bảng quy tắc hoàn tiền (20.5).
/// </summary>
public sealed class RefundPolicyRulesRequest
{
    public List<RefundPolicyRuleRequest> Rules { get; set; } = [];
}
