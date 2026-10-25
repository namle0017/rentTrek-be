using FluentValidation;
using RenTrek.BLL.DTOs.Users;

namespace RenTrek.BLL.Validators.Users;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Mật khẩu hiện tại không được để trống");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có tối thiểu 8 ký tự")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu mới phải có ít nhất 1 chữ hoa")
            .Matches(@"[a-z]").WithMessage("Mật khẩu mới phải có ít nhất 1 chữ thường")
            .Matches(@"[0-9]").WithMessage("Mật khẩu mới phải có ít nhất 1 chữ số")
            .Matches(@"[\W_]").WithMessage("Mật khẩu mới phải có ít nhất 1 ký tự đặc biệt (@, #, $, %, ...)")
            .Must((request, newPassword) => newPassword != request.CurrentPassword)
            .WithMessage("Mật khẩu mới không được trùng với mật khẩu hiện tại");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Vui lòng xác nhận mật khẩu mới")
            .Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu mới không khớp");
    }
}
