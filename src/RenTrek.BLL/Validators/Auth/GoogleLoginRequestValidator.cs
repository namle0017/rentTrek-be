using FluentValidation;
using RenTrek.BLL.DTOs.Auth.Requests;

namespace RenTrek.BLL.Validators.Auth;

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("Google ID Token không được để trống");
    }
}
