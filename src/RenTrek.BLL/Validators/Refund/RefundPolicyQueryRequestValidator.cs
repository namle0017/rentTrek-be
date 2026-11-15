using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.Refund;

/// <summary>
/// Kiểm tra bộ lọc danh sách chính sách hoàn tiền theo whitelist.
/// </summary>
public sealed class RefundPolicyQueryRequestValidator : AbstractValidator<RefundPolicyQueryRequest>
{
    private static readonly string[] SortFields = ["createdAt", "name", "effectiveFrom", "version"];

    public RefundPolicyQueryRequestValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("Từ khóa tìm kiếm tối đa 200 ký tự");
        RuleFor(x => x.SortBy)
            .Must(x => SortFields.Contains(x, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Sắp xếp theo phải là một trong: {string.Join(", ", SortFields)}");
        RuleFor(x => x.SortOrder)
            .Must(x => x is not null && (x.Equals("asc", StringComparison.OrdinalIgnoreCase) || x.Equals("desc", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Thứ tự sắp xếp phải là asc hoặc desc");
        RuleFor(x => x.PolicyType!).OneOf("Loại chính sách", RefundPolicyTypes.Tour, RefundPolicyTypes.Rental)
            .When(x => x.PolicyType != null);
        RuleFor(x => x.Status!).OneOf("Trạng thái", RefundPolicyStatuses.DisplayStatuses)
            .When(x => x.Status != null);
    }
}
