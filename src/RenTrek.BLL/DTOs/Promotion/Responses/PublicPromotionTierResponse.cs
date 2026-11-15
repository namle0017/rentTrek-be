namespace RenTrek.BLL.DTOs.Promotion.Responses;

/// <summary>Một bậc ưu đãi: đạt <see cref="MinThreshold"/> theo <see cref="QualificationMetric"/> thì giảm theo <see cref="DiscountType"/>.</summary>
public sealed record PublicPromotionTierResponse(
    string QualificationMetric,
    decimal MinThreshold,
    string DiscountType,
    decimal DiscountValue);
