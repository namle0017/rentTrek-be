namespace RenTrek.BLL.DTOs.Promotion.Internal;

/// <summary>
/// Loại giảm giá nhận diện được từ promotion_tiers.discount_type.
/// </summary>
public enum PromotionDiscountKind
{
    Unsupported,
    Percent,
    Fixed
}
