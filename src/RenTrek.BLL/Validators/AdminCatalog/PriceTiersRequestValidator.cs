using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>
/// Kiểm tra bảng bậc giá: bắt đầu từ ngày 1, liên tục, không chồng lấn, chỉ bậc cuối được để MaxDays trống, giá không âm.
/// </summary>
public sealed class PriceTiersRequestValidator : AbstractValidator<PriceTiersRequest>
{
    public PriceTiersRequestValidator()
    {
        RuleFor(x => x.Tiers)
            .Must(x => x is { Count: > 0 and <= 20 }).WithMessage("Cần từ 1 đến 20 bậc giá")
            .Must(BeContiguous)
            .WithMessage("Các bậc giá phải bắt đầu từ ngày 1, liên tục, không chồng lấn; chỉ bậc cuối được để trống số ngày tối đa");
        RuleForEach(x => x.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(t => t.MinDays).GreaterThanOrEqualTo(1).WithMessage("Số ngày tối thiểu phải từ 1");
            tier.RuleFor(t => t.MaxDays).Must((t, max) => max == null || max >= t.MinDays)
                .WithMessage("Số ngày tối đa phải lớn hơn hoặc bằng số ngày tối thiểu");
            tier.RuleFor(t => t.DailyPrice).GreaterThanOrEqualTo(0).WithMessage("Giá thuê/ngày không được âm");
        });
    }

    private static bool BeContiguous(List<PriceTierRequest>? tiers)
    {
        if (tiers is not { Count: > 0 }) return true;
        var ordered = tiers.OrderBy(t => t.MinDays).ToList();
        if (ordered[0].MinDays != 1) return false;
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            if (ordered[i].MaxDays is not { } max || ordered[i + 1].MinDays != max + 1) return false;
        }
        return true;
    }
}
