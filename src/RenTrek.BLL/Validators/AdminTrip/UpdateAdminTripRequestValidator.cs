using FluentValidation;
using RenTrek.BLL.DTOs.AdminTrip.Requests;

namespace RenTrek.BLL.Validators.AdminTrip;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu cập nhật chuyến đi.
/// </summary>
public sealed class UpdateAdminTripRequestValidator : AbstractValidator<UpdateAdminTripRequest>
{
    public UpdateAdminTripRequestValidator()
    {
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

        RuleFor(x => x.RowVersion)
            .GreaterThanOrEqualTo(1)
            .WithMessage("RowVersion là bắt buộc để kiểm tra đồng thời");
    }
}
