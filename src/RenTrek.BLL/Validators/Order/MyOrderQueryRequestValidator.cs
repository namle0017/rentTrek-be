using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Order.Requests;

namespace RenTrek.BLL.Validators.Order;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho my order query request.
/// </summary>
public class MyOrderQueryRequestValidator : AbstractValidator<MyOrderQueryRequest>
{
    public MyOrderQueryRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(200)
            .WithMessage("Từ khoá tìm kiếm tối đa 200 ký tự");
        RuleFor(request => request.OrderType)
            .Must(value => string.Equals(value?.Trim(), OrderTypes.Rental, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value?.Trim(), OrderTypes.TripBooking, StringComparison.OrdinalIgnoreCase))
            .When(request => !string.IsNullOrWhiteSpace(request.OrderType))
            .WithMessage("Loại đơn phải là Rental hoặc TripBooking");
        RuleFor(request => request.Status).MaximumLength(50)
            .WithMessage("Trạng thái đơn tối đa 50 ký tự");
        RuleFor(request => request.SortBy)
            .Must(value => new[] { "createdAt", "serviceStartAt" }
                .Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sắp xếp theo createdAt hoặc serviceStartAt");
        RuleFor(request => request.SortOrder)
            .Must(value => string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Thứ tự sắp xếp phải là asc hoặc desc");
        RuleFor(request => request.PageNumber)
            .Must((request, page) => (long)(page - 1) * request.PageSize <= int.MaxValue)
            .WithMessage("Số trang vượt giới hạn hỗ trợ");
    }
}
