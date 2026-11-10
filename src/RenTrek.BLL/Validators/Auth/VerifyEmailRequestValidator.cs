using FluentValidation;
using RenTrek.BLL.DTOs.Auth.Requests;

namespace RenTrek.BLL.Validators.Auth;

/// <summary>
/// Kiểm tra dữ liệu đầu vào khi xác thực email tài khoản.
/// Chấp nhận xác thực qua Token (link) HOẶC qua Email + OTP (modal).
/// </summary>
public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Token) || (!string.IsNullOrWhiteSpace(x.Otp) && !string.IsNullOrWhiteSpace(x.Email)))
            .WithMessage("Vui lòng cung cấp mã OTP hoặc link xác thực email");

        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email không đúng định dạng");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Otp), () =>
        {
            RuleFor(x => x.Otp)
                .Matches(@"^\d{6}$").WithMessage("Mã OTP phải gồm đúng 6 chữ số");
        });
    }
}
