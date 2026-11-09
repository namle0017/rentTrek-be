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
