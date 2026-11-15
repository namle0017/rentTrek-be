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
        When(x => x.PolicyType == RefundPolicyTypes.Tour, () =>
        {
            RuleFor(x => x.RescheduleMinHoursBefore).InclusiveBetween(0, 8760)
                .When(x => x.RescheduleMinHoursBefore.HasValue).WithMessage("Số giờ đổi lịch phải từ 0 đến 8760");
            RuleFor(x => x.RescheduleFee).InclusiveBetween(0, 100_000_000).WithMessage("Phí đổi lịch không hợp lệ");
            RuleFor(x => x.MaxReschedules).InclusiveBetween(0, 10).WithMessage("Số lần đổi lịch phải từ 0 đến 10");
            RuleFor(x => x.NoShowGraceMinutes).InclusiveBetween(0, 1440).WithMessage("Thời gian trễ cho phép phải từ 0 đến 1440 phút");
        });
    }
}
