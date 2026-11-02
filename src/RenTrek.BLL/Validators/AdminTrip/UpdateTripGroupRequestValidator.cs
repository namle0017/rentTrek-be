using FluentValidation;
using RenTrek.BLL.DTOs.AdminTrip.Requests;

namespace RenTrek.BLL.Validators.AdminTrip;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu sửa thông tin nhóm trong chuyến đi.
/// </summary>
public sealed class UpdateTripGroupRequestValidator : AbstractValidator<UpdateTripGroupRequest>
{
    public UpdateTripGroupRequestValidator()
    {
        RuleFor(x => x.GroupName)
            .NotEmpty().WithMessage("Tên nhóm là bắt buộc")
            .MaximumLength(200).WithMessage("Tên nhóm không vượt quá 200 ký tự");

        RuleFor(x => x.MaxParticipants)
            .GreaterThanOrEqualTo(1).WithMessage("Sĩ số tối đa của nhóm phải lớn hơn hoặc bằng 1");
    }
}
