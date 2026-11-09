using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra định mức đồ cấp: biến thể, số lượng &gt; 0, cơ sở tính hợp lệ (FR-T04).</summary>
public sealed class SaveTourProvidedItemRequestValidator : AbstractValidator<SaveTourProvidedItemRequest>
{
    public SaveTourProvidedItemRequestValidator()
    {
        RuleFor(x => x.ProductVariantId).NotEmpty().WithMessage("Biến thể không được để trống");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0");
        RuleFor(x => x.QuantityBasis).OneOf("Cơ sở tính", "PerPerson", "PerGroup", "PerTrip");
        RuleFor(x => x.Note).MaximumLength(1000).WithMessage("Ghi chú tối đa 1000 ký tự");
    }
}
