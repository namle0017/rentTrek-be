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
        RuleFor(x => x.EffectiveFrom).NotEqual(default(DateOnly)).WithMessage("Ngày hiệu lực không được để trống");
    }
}
