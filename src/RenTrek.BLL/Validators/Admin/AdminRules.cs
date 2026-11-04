using System.Text.RegularExpressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

// Chỉ kiểm định dạng. Quy tắc cần dữ liệu DB (trùng tên, trạng thái đơn, tự khóa mình...) nằm trong AdminService.

internal static class AdminRules
{
    public static IRuleBuilderOptions<T, string> Text<T>(this IRuleBuilder<T, string> rule, string label, int max)
        => rule.NotEmpty().WithMessage($"{label} không được để trống")
            .MaximumLength(max).WithMessage($"{label} tối đa {max} ký tự");

    public static IRuleBuilderOptions<T, string> OneOf<T>(this IRuleBuilder<T, string> rule, string label, params string[] values)
        => rule.Must(values.Contains).WithMessage($"{label} phải là một trong: {string.Join(", ", values)}");

    public static IRuleBuilderOptions<T, string?> Phone<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(x => string.IsNullOrWhiteSpace(x) || Regex.IsMatch(x.Trim(), @"^(03|05|07|08|09)\d{8}$"))
            .WithMessage("Số điện thoại không đúng định dạng (10 chữ số bắt đầu bằng 03, 05, 07, 08, 09)");

    public static IRuleBuilderOptions<T, DateOnly?> NotFuture<T>(this IRuleBuilder<T, DateOnly?> rule, string message)
        => rule.Must(x => x == null || x <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(message);
}
