using FluentValidation;
using RenTrek.BLL.DTOs.Users.Requests;

namespace RenTrek.BLL.Validators.Users;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho update profile request.
/// </summary>
public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống")
            .Length(2, 100).WithMessage("Họ và tên phải từ 2 đến 100 ký tự");

        RuleFor(x => x.Phone)
            .Matches(@"^(03|05|07|08|09)\d{8}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Số điện thoại không đúng định dạng (10 chữ số bắt đầu bằng 03, 05, 07, 08, 09)");
    }
}
