using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class AdminProfileRequestValidator : AbstractValidator<AdminProfileRequest>
{
    public AdminProfileRequestValidator()
    {
        RuleFor(x => x.FullName).Text("Họ và tên", 100);
        RuleFor(x => x.Phone).Phone();
        RuleFor(x => x.DateOfBirth).NotFuture("Ngày sinh không hợp lệ");
        RuleFor(x => x.CareerStartDate).NotFuture("Ngày vào nghề không được ở tương lai");
        RuleFor(x => x.Address).MaximumLength(500).WithMessage("Địa chỉ tối đa 500 ký tự");
    }
}
