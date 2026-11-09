using FluentValidation;
using RenTrek.BLL.DTOs.AdminTrip.Requests;

namespace RenTrek.BLL.Validators.AdminTrip;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu hủy chuyến đi.
/// </summary>
public sealed class CancelAdminTripRequestValidator : AbstractValidator<CancelAdminTripRequest>
{
    public CancelAdminTripRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Bắt buộc nhập lý do hủy chuyến")
            .MaximumLength(1000).WithMessage("Lý do hủy chuyến không vượt quá 1000 ký tự");
    }
}
