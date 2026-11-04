using System.Text.RegularExpressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên danh mục", 100);
        // Bắt buộc khi tạo mới: service kiểm vì validator không biết là tạo hay sửa.
        RuleFor(x => x.CategoryType!).OneOf("Loại danh mục", "Tour", "Equipment", "Bundle").When(x => x.CategoryType != null);
    }
}
