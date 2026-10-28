using FluentValidation;
using RenTrek.BLL.DTOs.RentalOrder;

namespace RenTrek.BLL.Validators.RentalOrder;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho rental quote request (định dạng giỏ thuê: 1-20 dòng, mỗi dòng số lượng 1-20).
/// Khoảng thời gian thuê và việc mỗi dòng có đúng một trong productVariantId / bundleId được kiểm ở service để trả đúng mã lỗi.
/// </summary>
public class RentalQuoteRequestValidator : AbstractValidator<RentalQuoteRequest>
{
    public const int MaxItems = 20;
    public const int MaxQuantity = 20;
    public const int MaxPromotionCodeLength = 50;

    public RentalQuoteRequestValidator()
    {
        RuleFor(request => request.RentalStartAt)
            .NotEqual(default(DateTime)).WithMessage("Thời điểm nhận là bắt buộc");

        RuleFor(request => request.RentalEndAt)
            .NotEqual(default(DateTime)).WithMessage("Thời điểm trả là bắt buộc");

        RuleFor(request => request.Items)
            .NotNull().WithMessage("Danh sách thiết bị thuê là bắt buộc")
            .Must(items => items is { Count: >= 1 and <= MaxItems })
            .WithMessage($"Danh sách thiết bị thuê phải có từ 1 đến {MaxItems} dòng");

        RuleForEach(request => request.Items)
            .NotNull().WithMessage("Dòng thuê không hợp lệ")
            .ChildRules(item => item.RuleFor(line => line.Quantity)
                .InclusiveBetween(1, MaxQuantity)
                .WithMessage($"Số lượng phải từ 1 đến {MaxQuantity}"));

        RuleFor(request => request.PromotionCode)
            .MaximumLength(MaxPromotionCodeLength)
            .WithMessage($"Mã khuyến mãi tối đa {MaxPromotionCodeLength} ký tự");
    }
}
