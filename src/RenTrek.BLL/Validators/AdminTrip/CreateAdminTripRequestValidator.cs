using FluentValidation;
using RenTrek.BLL.DTOs.AdminTrip.Requests;

namespace RenTrek.BLL.Validators.AdminTrip;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu tạo chuyến đi thủ công.
/// </summary>
public sealed class CreateAdminTripRequestValidator : AbstractValidator<CreateAdminTripRequest>
{
    public CreateAdminTripRequestValidator()
    {
        RuleFor(x => x.TourId)
            .NotEmpty().WithMessage("Mã tour là bắt buộc");

        RuleFor(x => x.RefundPolicyId)
            .NotEmpty().WithMessage("Mã chính sách hoàn tiền là bắt buộc");

        RuleFor(x => x.EndAt)
            .GreaterThan(x => x.StartAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian khởi hành");

        RuleFor(x => x.TotalSlots)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Tổng sức chứa phải lớn hơn hoặc bằng 1");

        RuleFor(x => x.MinParticipants)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số khách tối thiểu phải lớn hơn hoặc bằng 1")
            .LessThanOrEqualTo(x => x.TotalSlots)
            .WithMessage("Số khách tối thiểu không được vượt quá tổng sức chứa");

        RuleFor(x => x.PricePerPerson)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Giá mỗi người không được âm");
    }
}
