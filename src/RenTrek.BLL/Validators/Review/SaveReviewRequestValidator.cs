using FluentValidation;
using RenTrek.BLL.DTOs.Review.Requests;

namespace RenTrek.BLL.Validators.Review;

/// <summary>
/// Kiểm tra tính hợp lệ đầu vào khi tạo hoặc cập nhật đánh giá chuyến đi.
/// </summary>
public sealed class SaveReviewRequestValidator : AbstractValidator<SaveReviewRequest>
{
    public SaveReviewRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5)
            .WithMessage("Số sao đánh giá phải nằm trong khoảng từ 1 đến 5.");

        RuleFor(x => x.Comment)
            .MaximumLength(2000)
            .When(x => x.Comment is not null)
            .WithMessage("Nội dung đánh giá tối đa 2000 ký tự.");
    }
}
