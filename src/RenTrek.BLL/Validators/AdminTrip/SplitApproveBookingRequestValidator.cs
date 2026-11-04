using FluentValidation;
using RenTrek.BLL.DTOs.AdminTrip.Requests;

namespace RenTrek.BLL.Validators.AdminTrip;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu duyệt tách đoàn đi chung.
/// </summary>
public sealed class SplitApproveBookingRequestValidator : AbstractValidator<SplitApproveBookingRequest>
{
    public SplitApproveBookingRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Bắt buộc nhập lý do tách đoàn")
            .MaximumLength(1000).WithMessage("Lý do tách đoàn không vượt quá 1000 ký tự");
    }
}
