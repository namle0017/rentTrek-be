using FluentValidation;
using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Validators.Auth;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự");
    }
}
