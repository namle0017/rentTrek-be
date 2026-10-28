using FluentValidation;
using RenTrek.BLL.DTOs.ProductBundle.Requests;

namespace RenTrek.BLL.Validators.ProductBundle;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho product bundle query request.
/// Khoảng thuê from-to hợp lệ (to sau from, không ở quá khứ, tối đa 60 ngày) do service kiểm tra.
/// </summary>
public class ProductBundleQueryRequestValidator : AbstractValidator<ProductBundleQueryRequest>
{
    public ProductBundleQueryRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(200)
            .WithMessage("Từ khoá tìm kiếm tối đa 200 ký tự");
        RuleFor(request => request.CategoryId).NotEqual(Guid.Empty)
            .WithMessage("Danh mục không hợp lệ");
        RuleFor(request => request.SortBy)
            .Must(value => new[] { "createdAt", "name", "priceFrom" }
                .Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sắp xếp theo createdAt, name hoặc priceFrom");
        RuleFor(request => request.SortOrder)
            .Must(value => string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Thứ tự sắp xếp phải là asc hoặc desc");
        RuleFor(request => request.PageNumber)
            .Must((request, page) => (long)(page - 1) * request.PageSize <= int.MaxValue)
            .WithMessage("Số trang vượt giới hạn hỗ trợ");
    }
}
