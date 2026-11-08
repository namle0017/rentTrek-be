using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(x => x.Email).Text("Email", 255)
            .Must(x => System.Net.Mail.MailAddress.TryCreate(x.Trim(), out var address) && address.Address == x.Trim())
            .WithMessage("Email không đúng định dạng");
        RuleFor(x => x.FullName).Text("Họ và tên", 100);
        RuleFor(x => x.Phone).Phone();
        RuleFor(x => x.Role).OneOf("Vai trò", "Staff", "Guide");
        RuleFor(x => x.Password)
            .Length(8, 128).WithMessage("Mật khẩu từ 8 đến 128 ký tự")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ cái in hoa")
            .Matches("[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ cái in thường")
            .Matches("[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số")
            .Matches("[^a-zA-Z0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt");
        RuleFor(x => x.CareerStartDate).NotFuture("Ngày vào nghề không được ở tương lai");
    }
}
