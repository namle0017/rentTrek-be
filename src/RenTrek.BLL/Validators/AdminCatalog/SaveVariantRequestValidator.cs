using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body tạo / sửa biến thể.</summary>
public sealed class SaveVariantRequestValidator : AbstractValidator<SaveVariantRequest>
{
    public SaveVariantRequestValidator()
    {
        RuleFor(x => x.Sku).Text("SKU", 100)
            .Matches(@"^[A-Za-z0-9][A-Za-z0-9\-_.]*$").WithMessage("SKU chỉ gồm chữ, số, '-', '_', '.'");
        RuleFor(x => x.VariantName).Text("Tên biến thể", 255);
        RuleFor(x => x.Size).MaximumLength(50).WithMessage("Size tối đa 50 ký tự");
        RuleFor(x => x.Color).MaximumLength(50).WithMessage("Màu tối đa 50 ký tự");
        RuleFor(x => x.Status).OneOf("Trạng thái", CatalogStatusValues.All);
    }
}
