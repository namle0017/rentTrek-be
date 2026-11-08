using FluentValidation;
using RenTrek.BLL.DTOs.RentalOps.Requests;

namespace RenTrek.BLL.Validators.RentalOps;

public class ScanHandoverRequestValidator : AbstractValidator<ScanHandoverRequest>
{
    public ScanHandoverRequestValidator()
    {
        RuleFor(x => x.AssetCode)
            .NotEmpty().WithMessage("Vui lòng quét hoặc nhập mã thiết bị.")
            .MaximumLength(100).WithMessage("Mã thiết bị tối đa 100 ký tự.");
    }
}
