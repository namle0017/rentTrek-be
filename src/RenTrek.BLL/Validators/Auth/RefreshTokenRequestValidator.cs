using FluentValidation;
using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Validators.Auth;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        // TODO: them rule
    }
}
