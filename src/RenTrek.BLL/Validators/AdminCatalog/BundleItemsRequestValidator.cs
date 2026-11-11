using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra thành phần combo: ít nhất 1 món, số lượng &gt; 0, không trùng biến thể.</summary>
public sealed class BundleItemsRequestValidator : AbstractValidator<BundleItemsRequest>
{
    public BundleItemsRequestValidator()
    {
        RuleFor(x => x.Items)
            .Must(x => x is { Count: > 0 and <= 50 }).WithMessage("Combo cần từ 1 đến 50 thành phần")
            .Must(x => x == null || x.Select(i => i.ProductVariantId).Distinct().Count() == x.Count)
            .WithMessage("Không được chọn trùng một biến thể trong cùng combo");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductVariantId).NotEmpty().WithMessage("Biến thể không được để trống");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0");
        });
    }
}
