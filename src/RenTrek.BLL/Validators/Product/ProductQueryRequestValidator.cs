using FluentValidation;
using RenTrek.BLL.DTOs.Product.Requests;

namespace RenTrek.BLL.Validators.Product;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho product query request.
/// </summary>
public class ProductQueryRequestValidator : AbstractValidator<ProductQueryRequest>
{
    private static readonly string[] AllowedOfferingTypes = ["Rental", "Sale", "Both"];
    private static readonly string[] AllowedSortFields = ["createdAt", "name", "pricePerDay"];

    public ProductQueryRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(200)
            .WithMessage("Từ khoá tìm kiếm tối đa 200 ký tự");
        RuleFor(request => request.CategoryId).NotEqual(Guid.Empty)
            .WithMessage("Danh mục không hợp lệ");
        RuleFor(request => request.OfferingType)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || AllowedOfferingTypes.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Hình thức cung cấp phải là Rental, Sale hoặc Both");
        RuleFor(request => request.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(request => request.MinPrice.HasValue)
            .WithMessage("Giá tối thiểu không được âm");
        RuleFor(request => request.MaxPrice)
            .GreaterThanOrEqualTo(0)
            .When(request => request.MaxPrice.HasValue)
            .WithMessage("Giá tối đa không được âm");
        RuleFor(request => request.MaxPrice)
            .GreaterThanOrEqualTo(request => request.MinPrice!.Value)
            .When(request => request.MinPrice.HasValue && request.MaxPrice.HasValue)
            .WithMessage("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu");
        RuleFor(request => request.SortBy)
            .Must(value => AllowedSortFields.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sắp xếp theo createdAt, name hoặc pricePerDay");
        RuleFor(request => request.SortOrder)
            .Must(value => string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Thứ tự sắp xếp phải là asc hoặc desc");
        RuleFor(request => request.PageNumber)
            .Must((request, page) => (long)(page - 1) * request.PageSize <= int.MaxValue)
            .WithMessage("Số trang vượt giới hạn hỗ trợ");
    }
}
