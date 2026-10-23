using FluentValidation;
using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // TODO: them rule
    }
}
