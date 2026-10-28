namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Một bậc khuyến mãi (promotion_tiers) dùng để chọn bậc đủ điều kiện.
/// </summary>
public class PromotionTierOption
{
    public Guid TierId { get; set; }
    public decimal MinThreshold { get; set; }
    public string QualificationMetric { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }

    /// <summary>Bậc tặng quà: chưa hỗ trợ nên không bao giờ được chọn.</summary>
    public bool IsGift { get; set; }
}
