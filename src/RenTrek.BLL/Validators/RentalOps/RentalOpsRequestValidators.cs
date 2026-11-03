using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOps.Requests;

namespace RenTrek.BLL.Validators.RentalOps;

public class ReturnRentalAssetsRequestValidator : AbstractValidator<ReturnRentalAssetsRequest>
{
    public ReturnRentalAssetsRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Chọn ít nhất một thiết bị để nhận trả.")
            .Must(items => items.Select(i => i.AssignmentId).Distinct().Count() == items.Count)
            .WithMessage("Mỗi thiết bị chỉ được nhận trả một lần trong một lượt.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.AssignmentId).NotEmpty().WithMessage("Thiếu thiết bị.");
            item.RuleFor(i => i.Condition)
                .Must(c => ReturnConditions.All.Contains(c))
                .WithMessage("Tình trạng trả không hợp lệ.");
            item.RuleFor(i => i.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
        });
    }
}

public class CreateRentalChargeRequestValidator : AbstractValidator<CreateRentalChargeRequest>
{
    public CreateRentalChargeRequestValidator()
    {
        RuleFor(x => x.ChargeType)
            .Must(t => RentalChargeTypes.All.Contains(t))
            .WithMessage("Loại phí không hợp lệ.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phí phải lớn hơn 0.")
            .LessThanOrEqualTo(1_000_000_000).WithMessage("Số tiền phí quá lớn.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do.")
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");

        // Phí hỏng / mất phải truy vết được thiết bị chịu phí (FR-C03)
        RuleFor(x => x.AssignmentId)
            .NotNull()
            .When(x => x.ChargeType is RentalChargeTypes.Damage or RentalChargeTypes.Loss)
            .WithMessage("Phí hỏng / mất phải chọn thiết bị chịu phí.");
    }
}

public class VoidRentalChargeRequestValidator : AbstractValidator<VoidRentalChargeRequest>
{
    public VoidRentalChargeRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do hủy phí.")
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}
