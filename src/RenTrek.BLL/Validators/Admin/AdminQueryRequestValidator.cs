using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class AdminQueryRequestValidator : AbstractValidator<AdminQueryRequest>
{
    public AdminQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.SortOrder).Must(x => x is "asc" or "desc");
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).When(x => x.Rating.HasValue);
        RuleFor(x => x.GroupBy).Must(x => x is "month" or "week");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From.HasValue && x.To.HasValue);
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).When(x => x.FromDate.HasValue && x.ToDate.HasValue);
    }
}
