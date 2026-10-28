using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Promotion.Internal;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Tính khuyến mãi dạng giảm tiền (thuần, không truy cập DB). Chỉ giảm tiền, không tặng quà.
/// </summary>
public static class PromotionDiscountCalculator
{
    /// <summary>Nhận diện loại giảm giá từ promotion_tiers.discount_type.</summary>
    public static PromotionDiscountKind GetDiscountKind(string? discountType)
    {
        if (PromotionRules.Matches(discountType, PromotionRules.DiscountPercent))
            return PromotionDiscountKind.Percent;

        return PromotionRules.Matches(discountType, PromotionRules.DiscountFixed)
            ? PromotionDiscountKind.Fixed
            : PromotionDiscountKind.Unsupported;
    }

    /// <summary>Giá trị của metric trên giỏ; null nghĩa là không tính được (bậc không đủ điều kiện).</summary>
    public static decimal? GetMetricValue(string? metric, decimal subtotal, int totalQuantity, int billableDays)
    {
        if (PromotionRules.Matches(metric, PromotionRules.MetricAmount))
            return subtotal;

        if (PromotionRules.Matches(metric, PromotionRules.MetricQuantity))
            return totalQuantity;

        if (PromotionRules.Matches(metric, PromotionRules.MetricDays) && billableDays > 0)
            return billableDays;

        return null;
    }

    /// <summary>
    /// Chọn bậc cao nhất đủ điều kiện: bỏ bậc tặng quà, loại giảm không hỗ trợ, metric không tính được, chưa đạt ngưỡng
    /// (ngưỡng tính trên giá trị TRƯỚC giảm). Trong các bậc còn lại chọn MinThreshold lớn nhất, hoà thì giảm nhiều hơn.
    /// </summary>
    public static PromotionTierOption? SelectTier(
        IEnumerable<PromotionTierOption> tiers,
        decimal subtotal,
        int totalQuantity,
        int billableDays)
        => tiers
            .Where(t => !t.IsGift
                        && GetDiscountKind(t.DiscountType) != PromotionDiscountKind.Unsupported
                        && GetMetricValue(t.QualificationMetric, subtotal, totalQuantity, billableDays) is { } value
                        && value >= t.MinThreshold)
            .OrderByDescending(t => t.MinThreshold)
            .ThenByDescending(t => t.DiscountValue)
            .ThenBy(t => t.TierId)
            .FirstOrDefault();

    /// <summary>
    /// Giảm cấp đơn: phần trăm thì floor(base × pct / 100) (pct kẹp 0–100); số tiền cố định thì min(amount, base).
    /// Luôn nằm trong [0, base].
    /// </summary>
    public static decimal CalculateOrderDiscount(string? discountType, decimal discountValue, decimal baseAmount)
    {
        if (baseAmount <= 0 || discountValue <= 0)
            return 0;

        var discount = GetDiscountKind(discountType) switch
        {
            PromotionDiscountKind.Percent => Math.Floor(baseAmount * Math.Min(discountValue, 100m) / 100m),
            PromotionDiscountKind.Fixed => Math.Min(Math.Floor(discountValue), baseAmount),
            _ => 0m
        };

        return Math.Clamp(discount, 0, baseAmount);
    }

    /// <summary>
    /// Giảm theo từng dòng (phạm vi Line). Phần trăm: floor(lineAmount × pct / 100) từng dòng. Số tiền cố định: tổng
    /// min(amount, Σ dòng) phân bổ tỉ lệ theo tiền dòng, phần dư làm tròn dồn cho các dòng có phần lẻ lớn nhất nên tổng
    /// khớp tuyệt đối. Mỗi dòng không giảm quá tiền dòng. Chỉ trả các dòng có giảm lớn hơn 0.
    /// </summary>
    public static List<PromotionLineDiscountItem> CalculateLineDiscounts(
        string? discountType,
        decimal discountValue,
        IReadOnlyList<PromotionLineInput> lines)
    {
        var positive = lines.Where(l => l.LineAmount > 0).ToList();
        var result = new List<PromotionLineDiscountItem>();
        if (positive.Count == 0 || discountValue <= 0)
            return result;

        var kind = GetDiscountKind(discountType);
        if (kind == PromotionDiscountKind.Percent)
        {
            var percent = Math.Min(discountValue, 100m);
            foreach (var line in positive)
            {
                var amount = Math.Floor(line.LineAmount * percent / 100m);
                if (amount > 0)
                    result.Add(new PromotionLineDiscountItem { LineIndex = line.Index, Amount = amount });
            }

            return result;
        }

        if (kind != PromotionDiscountKind.Fixed)
            return result;

        var sum = positive.Sum(l => l.LineAmount);
        var total = Math.Min(Math.Floor(discountValue), sum);
        var shares = positive
            .Select(l => new { l.Index, l.LineAmount, Exact = total * l.LineAmount / sum })
            .Select(x => new
            {
                x.Index,
                x.LineAmount,
                Floor = Math.Floor(x.Exact),
                Remainder = x.Exact - Math.Floor(x.Exact)
            })
            .ToList();

        var leftover = total - shares.Sum(s => s.Floor);
        var bonusIndexes = shares
            .OrderByDescending(s => s.Remainder)
            .ThenBy(s => s.Index)
            .Take((int)leftover)
            .Select(s => s.Index)
            .ToHashSet();

        foreach (var share in shares)
        {
            var amount = Math.Min(share.LineAmount, share.Floor + (bonusIndexes.Contains(share.Index) ? 1 : 0));
            if (amount > 0)
                result.Add(new PromotionLineDiscountItem { LineIndex = share.Index, Amount = amount });
        }

        return result;
    }

    /// <summary>
    /// Tính kết quả giảm giá cho một bậc đã chọn. Phạm vi theo dòng: chia theo dòng và DiscountAmount là tổng các dòng;
    /// phạm vi cấp đơn: DiscountAmount tính trên subtotal, LineDiscounts rỗng.
    /// </summary>
    public static PromotionDiscountResult Calculate(
        string? discountType,
        decimal discountValue,
        bool isLineScope,
        decimal subtotal,
        IReadOnlyList<PromotionLineInput> lines)
    {
        if (!isLineScope)
        {
            return new PromotionDiscountResult
            {
                DiscountAmount = CalculateOrderDiscount(discountType, discountValue, subtotal)
            };
        }

        var lineDiscounts = CalculateLineDiscounts(discountType, discountValue, lines);
        return new PromotionDiscountResult
        {
            DiscountAmount = lineDiscounts.Sum(d => d.Amount),
            LineDiscounts = lineDiscounts
        };
    }
}
