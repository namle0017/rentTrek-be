using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Combo gợi ý: không trùng combo, thứ tự không âm, ghi chú tối đa 500 ký tự.</summary>
public sealed class TourSuggestedBundlesRequestValidator : AbstractValidator<TourSuggestedBundlesRequest>
{
    public TourSuggestedBundlesRequestValidator()
    {
        RuleFor(x => x.Items).Must(items => items.Select(i => i.BundleId).Distinct().Count() == items.Count)
            .WithMessage("Danh sách combo bị trùng");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.BundleId).NotEmpty().WithMessage("Thiếu combo");
            item.RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị không được âm");
            item.RuleFor(x => x.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự");
        });
    }
}
