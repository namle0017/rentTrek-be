using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Post.Requests;

namespace RenTrek.BLL.Validators.Post;

public class PostQueryRequestValidator : AbstractValidator<PostQueryRequest>
{
    public PostQueryRequestValidator()
    {
        RuleFor(x => x.PostType)
            .Must(t => PostTypes.All.Contains(t))
            .When(x => !string.IsNullOrWhiteSpace(x.PostType))
            .WithMessage("Loại bài không hợp lệ.");
    }
}

public class AdminPostQueryRequestValidator : AbstractValidator<AdminPostQueryRequest>
{
    public AdminPostQueryRequestValidator()
    {
        Include(new PostQueryRequestValidator());

        RuleFor(x => x.Status)
            .Must(s => PostStatuses.All.Contains(s))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Trạng thái bài viết không hợp lệ.");
    }
}

public class UpsertPostRequestValidator : AbstractValidator<UpsertPostRequest>
{
    public UpsertPostRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MaximumLength(255).WithMessage("Tiêu đề tối đa 255 ký tự.");

        RuleFor(x => x.Slug)
            .MaximumLength(255).WithMessage("Đường dẫn tối đa 255 ký tự.");

        RuleFor(x => x.PostType)
            .Must(t => PostTypes.All.Contains(t))
            .WithMessage("Vui lòng chọn loại bài.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung không được để trống.")
            .MaximumLength(100_000).WithMessage("Nội dung quá dài.");

        RuleFor(x => x.TagIds)
            .Must(t => t.Count <= 10).WithMessage("Tối đa 10 tag cho một bài.");

        RuleFor(x => x.SoftSkillIds)
            .Must(s => s.Count <= 20).WithMessage("Tối đa 20 kỹ năng cho một bài.");

        RuleFor(x => x.TourIds)
            .Must(t => t.Count <= 20).WithMessage("Tối đa 20 tour cho một bài.");
    }
}
