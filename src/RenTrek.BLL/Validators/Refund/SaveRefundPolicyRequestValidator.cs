using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.Refund;

/// <summary>
/// Kiểm tra body tạo / sửa chính sách hoàn tiền.
/// </summary>
public sealed class SaveRefundPolicyRequestValidator : AbstractValidator<SaveRefundPolicyRequest>
{
    public SaveRefundPolicyRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên chính sách", 200);
        RuleFor(x => x.PolicyType).OneOf("Loại chính sách", RefundPolicyTypes.Tour, RefundPolicyTypes.Rental);
        RuleFor(x => x.Description).MaximumLength(2000).WithMessage("Mô tả tối đa 2000 ký tự");
        RuleFor(x => x.EffectiveFrom).NotEqual(default(DateOnly)).WithMessage("Ngày hiệu lực không được để trống");
        RuleFor(x => x.EffectiveUntil)
            .Must((x, until) => until == null || until > x.EffectiveFrom)
            .WithMessage("Ngày hết hiệu lực phải sau ngày bắt đầu");
        this.AddRuleListRules(x => x.Rules);
    }
}
