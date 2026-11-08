using FluentValidation;
using RenTrek.BLL.DTOs.Refund.Requests;

namespace RenTrek.BLL.Validators.Refund;

/// <summary>
/// Kiểm tra bảng bậc hoàn tiền: số giờ không trùng, phần trăm 0–100 và không tăng khi hủy sát giờ hơn.
/// </summary>
public sealed class RefundPolicyRulesRequestValidator : AbstractValidator<RefundPolicyRulesRequest>
{
    public RefundPolicyRulesRequestValidator()
    {
        RuleFor(x => x.Rules)
            .Must(x => x is { Count: > 0 and <= 20 }).WithMessage("Cần từ 1 đến 20 quy tắc hoàn tiền")
            .Must(x => x == null || x.Select(r => r.MinHoursBefore).Distinct().Count() == x.Count)
            .WithMessage("Số giờ trước khởi hành không được trùng")
            .Must(NotIncreaseCloserToStart)
            .WithMessage("Phần trăm hoàn không được tăng khi hủy sát giờ khởi hành hơn");
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.MinHoursBefore).GreaterThanOrEqualTo(0).WithMessage("Số giờ không được âm");
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
