using FluentValidation;
using RenTrek.BLL.DTOs.Product;

namespace RenTrek.BLL.Validators.Product;

public class ProductQueryRequestValidator : AbstractValidator<ProductQueryRequest>
{
    public ProductQueryRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(200)
            .WithMessage("Từ khoá tìm kiếm tối đa 200 ký tự");
        RuleFor(request => request.CategoryId).NotEqual(Guid.Empty)
            .WithMessage("Danh mục không hợp lệ");
        RuleFor(request => request.OfferingType).IsInEnum()
            .WithMessage("Hình thức phải là Rental, Sale hoặc Both");
        RuleFor(request => request.SortBy)
            .Must(value => new[] { "createdAt", "name", "pricePerDay", "salePrice" }
                .Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sắp xếp theo createdAt, name, pricePerDay hoặc salePrice");
        RuleFor(request => request.SortOrder)
            .Must(value => string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Thứ tự sắp xếp phải là asc hoặc desc");
        RuleFor(request => request.PageNumber)
            .Must((request, page) => (long)(page - 1) * request.PageSize <= int.MaxValue)
            .WithMessage("Số trang vượt giới hạn hỗ trợ");
    }
}
