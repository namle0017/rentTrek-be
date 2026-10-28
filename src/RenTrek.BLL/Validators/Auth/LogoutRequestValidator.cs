using FluentValidation;
using RenTrek.BLL.DTOs.Auth.Requests;

namespace RenTrek.BLL.Validators.Auth;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho logout request.
/// </summary>
public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token không được để trống");
    }
}
