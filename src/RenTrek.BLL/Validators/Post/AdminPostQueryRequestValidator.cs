using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Post.Requests;

namespace RenTrek.BLL.Validators.Post;

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
