using System.Text.RegularExpressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class PromotionTiersRequestValidator : AbstractValidator<PromotionTiersRequest>
{
    public PromotionTiersRequestValidator()
    {
        RuleFor(x => x.Tiers)
            .Must(x => x is { Count: > 0 and <= 50 }).WithMessage("Cần từ 1 đến 50 bậc ưu đãi")
            .Must(x => x == null || x.Select(t => (t.QualificationMetric, t.MinThreshold)).Distinct().Count() == x.Count)
            .WithMessage("Không được trùng điều kiện ưu đãi");
        RuleForEach(x => x.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(t => t.QualificationMetric).OneOf("Điều kiện", "OrderAmount", "ParticipantCount", "RentalDays");
            tier.RuleFor(t => t.DiscountType).OneOf("Kiểu ưu đãi", "Percentage", "FixedAmount", "GiftItem");
            tier.RuleFor(t => t.MinThreshold)
                .Must((t, v) => v >= 0 && (t.QualificationMetric == "OrderAmount" || v == decimal.Truncate(v)))
                .WithMessage("Điều kiện phải là số không âm hợp lệ");
            tier.RuleFor(t => t.DiscountValue)
                .Must((t, v) => v >= 0 && (t.DiscountType != "Percentage" || v <= 100))
                .WithMessage("Giá trị giảm không hợp lệ");
            tier.RuleFor(t => t.GiftProductVariantId)
                .Must((t, id) => t.DiscountType == "GiftItem"
                    ? id.HasValue && t.GiftQuantity > 0
                    : id == null && t.GiftQuantity == null)
                .WithMessage("Chỉ bậc GiftItem có quà tặng, và cần biến thể cùng số lượng dương");
        });
    }
}
