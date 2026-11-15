using System.Linq.Expressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Refund.Requests;

namespace RenTrek.BLL.Validators.Refund;

/// <summary>
/// Quy tắc dùng chung cho danh sách bậc hoàn tiền: 1–20 bậc, giờ khác nhau trong 0–8760, phần trăm 0–100
/// và không tăng khi hủy sát giờ khởi hành hơn.
/// </summary>
internal static class RefundPolicyRuleListRules
{
    public const int MaxRules = 20;
    public const int MaxHours = 8760;

    public static void AddRuleListRules<T>(
        this AbstractValidator<T> validator, Expression<Func<T, List<RefundPolicyRuleRequest>>> rules)
    {
        validator.RuleFor(rules)
            .Must(x => x is { Count: > 0 and <= MaxRules }).WithMessage($"Cần từ 1 đến {MaxRules} quy tắc hoàn tiền")
            .Must(x => x == null || x.Select(r => r.MinHoursBefore).Distinct().Count() == x.Count)
            .WithMessage("Số giờ trước khởi hành không được trùng")
            .Must(x => x == null || x.Count == 0 || x.Any(r => r.MinHoursBefore == 0))
            .WithMessage("Cần có mốc 0 giờ để huỷ sát giờ khởi hành luôn khớp một mức hoàn")
            .Must(NotIncreaseCloserToStart)
            .WithMessage("Phần trăm hoàn không được tăng khi hủy sát giờ khởi hành hơn");
        var items = Expression.Lambda<Func<T, IEnumerable<RefundPolicyRuleRequest>>>(rules.Body, rules.Parameters);
        validator.RuleForEach<RefundPolicyRuleRequest>(items).ChildRules(rule =>
        {
            rule.RuleFor(r => r.MinHoursBefore).InclusiveBetween(0, MaxHours)
                .WithMessage($"Số giờ phải từ 0 đến {MaxHours}");
            rule.RuleFor(r => r.RefundPercentage).InclusiveBetween(0, 100).WithMessage("Phần trăm hoàn phải từ 0 đến 100");
        });
    }

    private static bool NotIncreaseCloserToStart(List<RefundPolicyRuleRequest>? rules)
    {
        if (rules == null) return true;
        var ordered = rules.OrderByDescending(r => r.MinHoursBefore).ToList();
        return ordered.Zip(ordered.Skip(1)).All(pair => pair.Second.RefundPercentage <= pair.First.RefundPercentage);
    }
}
