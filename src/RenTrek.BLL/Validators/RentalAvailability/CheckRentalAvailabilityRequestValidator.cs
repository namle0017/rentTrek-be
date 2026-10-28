using FluentValidation;
using RenTrek.BLL.DTOs.RentalAvailability;

namespace RenTrek.BLL.Validators.RentalAvailability;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho check rental availability request.
/// Hình dạng từng dòng (variant / bundle) và khoảng thuê hợp lệ do service kiểm tra (INVALID_RENTAL_ITEM, INVALID_RENTAL_PERIOD).
/// </summary>
public class CheckRentalAvailabilityRequestValidator : AbstractValidator<CheckRentalAvailabilityRequest>
{
    public const int MaxItems = 20;
    public const int MaxQuantity = 20;

    public CheckRentalAvailabilityRequestValidator()
    {
        RuleFor(request => request.RentalStartAt).NotEmpty()
            .WithMessage("Thời điểm nhận là bắt buộc");
        RuleFor(request => request.RentalEndAt).NotEmpty()
            .WithMessage("Thời điểm trả là bắt buộc");
        RuleFor(request => request.Items)
            .NotNull().WithMessage("Danh sách thiết bị là bắt buộc")
            .Must(items => items is { Count: >= 1 and <= MaxItems })
            .WithMessage($"Giỏ thuê phải có từ 1 đến {MaxItems} dòng");
        RuleForEach(request => request.Items).ChildRules(item =>
            item.RuleFor(line => line.Quantity).InclusiveBetween(1, MaxQuantity)
                .WithMessage($"Số lượng phải từ 1 đến {MaxQuantity}"));
    }
}
