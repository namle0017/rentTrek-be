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
