using FluentValidation;
using RenTrek.BLL.DTOs.Tour;

namespace RenTrek.BLL.Validators.Tour;

public class TourQueryRequestValidator : AbstractValidator<TourQueryRequest>
{
    public TourQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số lượng bản ghi mỗi trang từ 1 đến 100.");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinPrice.HasValue)
            .WithMessage("Giá tối thiểu không được âm.");

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice ?? 0)
            .When(x => x.MaxPrice.HasValue)
            .WithMessage("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu.");

        RuleFor(x => x.DurationDays)
            .GreaterThan(0)
            .When(x => x.DurationDays.HasValue)
            .WithMessage("Số ngày tour phải lớn hơn 0.");
    }
}
