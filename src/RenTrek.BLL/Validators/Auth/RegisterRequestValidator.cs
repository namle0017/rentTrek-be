using FluentValidation;
using RenTrek.BLL.DTOs.Auth.Requests;

namespace RenTrek.BLL.Validators.Auth;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho register request.
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không đúng định dạng")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống")
            .MinimumLength(8).WithMessage("Mật khẩu phải chứa ít nhất 8 ký tự")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ cái in hoa")
            .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ cái in thường")
            .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Mật khẩu xác nhận không được để trống")
            .Equal(x => x.Password).WithMessage("Mật khẩu xác nhận không khớp");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống")
            .Length(2, 100).WithMessage("Họ và tên phải từ 2 đến 100 ký tự");

        RuleFor(x => x.Phone)
            .Matches(@"^(03|05|07|08|09)\d{8}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Số điện thoại không đúng định dạng (10 chữ số bắt đầu bằng 03, 05, 07, 08, 09)");
    }
}
