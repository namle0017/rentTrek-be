using FluentValidation;
using RenTrek.BLL.DTOs.Refund.Requests;

namespace RenTrek.BLL.Validators.Refund;

/// <summary>
/// Kiểm tra bảng bậc hoàn tiền khi thay riêng (20.5); quy tắc dùng chung với tạo / sửa chính sách.
/// </summary>
public sealed class RefundPolicyRulesRequestValidator : AbstractValidator<RefundPolicyRulesRequest>
{
    public RefundPolicyRulesRequestValidator()
    {
        this.AddRuleListRules(x => x.Rules);
    }
}
