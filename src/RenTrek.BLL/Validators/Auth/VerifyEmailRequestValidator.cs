using FluentValidation;
using RenTrek.BLL.DTOs.Auth.Requests;

namespace RenTrek.BLL.Validators.Auth;

/// <summary>
/// Kiểm tra dữ liệu đầu vào khi xác thực email tài khoản.
/// </summary>
public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Mã xác thực email không được để trống");
    }
}

