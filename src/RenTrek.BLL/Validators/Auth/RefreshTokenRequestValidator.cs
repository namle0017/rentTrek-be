using FluentValidation;
using RenTrek.BLL.DTOs.Auth;

namespace RenTrek.BLL.Validators.Auth;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho refresh token request.
/// </summary>
public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token không được để trống");
    }
}
