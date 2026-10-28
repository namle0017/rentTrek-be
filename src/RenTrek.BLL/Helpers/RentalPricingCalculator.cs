using RenTrek.BLL.DTOs.Rental;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Tính giá thuê theo bậc giá (thuần, không truy cập DB).
/// </summary>
public static class RentalPricingCalculator
{
    /// <summary>
    /// Chọn bậc có MinDays ≤ billableDays ≤ MaxDays (MaxDays null là vô hạn). Nếu nhiều bậc chồng nhau thì chọn
    /// bậc MinDays lớn nhất (cụ thể nhất), hoà thì theo PriceTierId nhỏ nhất. Không có bậc phù hợp thì trả null.
    /// </summary>
    public static RentalPriceTierOption? SelectTier(IEnumerable<RentalPriceTierOption> tiers, int billableDays)
        => tiers
            .Where(t => t.MinDays <= billableDays && (t.MaxDays is null || billableDays <= t.MaxDays))
            .OrderByDescending(t => t.MinDays)
            .ThenBy(t => t.PriceTierId)
            .FirstOrDefault();

    /// <summary>Thành tiền dòng = quantity × billableDays × dailyPrice.</summary>
    public static decimal CalculateLineAmount(int quantity, int billableDays, decimal dailyPrice)
        => (decimal)quantity * billableDays * dailyPrice;
}
