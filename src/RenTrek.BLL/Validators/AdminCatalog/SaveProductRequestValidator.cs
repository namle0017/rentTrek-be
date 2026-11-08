using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body tạo / sửa sản phẩm.</summary>
public sealed class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên sản phẩm", 255);
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Danh mục không được để trống");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("Mô tả tối đa 4000 ký tự");
        RuleFor(x => x.Status).OneOf("Trạng thái", CatalogStatusValues.All);
    }
}
