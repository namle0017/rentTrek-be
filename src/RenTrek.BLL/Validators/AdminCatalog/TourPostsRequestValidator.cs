using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra danh sách bài viết gắn tour: không trùng bài, thứ tự không âm.</summary>
public sealed class TourPostsRequestValidator : AbstractValidator<TourPostsRequest>
{
    public TourPostsRequestValidator()
    {
        RuleFor(x => x.Items).Must(items => items.Select(i => i.PostId).Distinct().Count() == items.Count)
            .WithMessage("Danh sách bài viết bị trùng");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PostId).NotEmpty().WithMessage("Thiếu bài viết");
            item.RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị không được âm");
        });
    }
}
