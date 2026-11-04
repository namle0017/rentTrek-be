using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOps.Requests;

namespace RenTrek.BLL.Validators.RentalOps;

public class VoidRentalChargeRequestValidator : AbstractValidator<VoidRentalChargeRequest>
{
    public VoidRentalChargeRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do hủy phí.")
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}
