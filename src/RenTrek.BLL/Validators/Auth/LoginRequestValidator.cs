using FluentValidation;
using RenTrek.BLL.DTOs.Auth.Requests;

namespace RenTrek.BLL.Validators.Auth;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho login request.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không đúng định dạng");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống");
    }
}
