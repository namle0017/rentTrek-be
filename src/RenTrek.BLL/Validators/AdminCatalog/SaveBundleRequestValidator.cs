using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body tạo / sửa combo.</summary>
public sealed class SaveBundleRequestValidator : AbstractValidator<SaveBundleRequest>
{
    public SaveBundleRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên combo", 255);
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Danh mục không được để trống");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("Mô tả tối đa 4000 ký tự");
        RuleFor(x => x.Status).OneOf("Trạng thái", CatalogStatusValues.All);
    }
}
