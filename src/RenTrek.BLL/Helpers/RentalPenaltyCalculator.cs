using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Công thức cọc và phạt của đơn thuê (docs/03.1 mục Cọc &amp; hoàn tiền).
/// Ngày trễ = số khối đủ 24 giờ kể từ mốc (giờ nhận hoặc hạn trả). Mọi % phạt tính trên tiền cọc.
/// </summary>
public static class RentalPenaltyCalculator
{
    private const decimal DepositRoundingUnit = 1000m;

    /// <summary>Cọc = % × tiền thuê trước giảm giá, làm tròn lên 1.000đ.</summary>
    public static decimal CalculateDeposit(decimal subtotalAmount, decimal depositPercent)
    {
        if (subtotalAmount <= 0 || depositPercent <= 0)
            return 0;

        var raw = subtotalAmount * depositPercent / 100m;
        return Math.Ceiling(raw / DepositRoundingUnit) * DepositRoundingUnit;
    }

    /// <summary>Số ngày trễ đủ 24 giờ kể từ <paramref name="anchorUtc"/>; 0 nếu chưa tới mốc.</summary>
    public static int FullDaysLate(DateTime anchorUtc, DateTime nowUtc)
        => nowUtc <= anchorUtc ? 0 : (int)Math.Floor((nowUtc - anchorUtc).TotalHours / 24d);

    /// <summary>Bậc có MinDaysLate lớn nhất không vượt số ngày trễ; null nếu chưa tới bậc nào.</summary>
    public static RentalPenaltyTier? FindTier(IReadOnlyList<RentalPenaltyTier> tiers, int daysLate)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        return tiers.Where(t => t.MinDaysLate <= daysLate).MaxBy(t => t.MinDaysLate);
    }

    /// <summary>Phí của một bậc: bậc terminal = trọn cọc, còn lại = % cọc làm tròn tới đồng.</summary>
    public static decimal TierFee(decimal depositAmount, RentalPenaltyTier? tier)
    {
        if (tier is null || depositAmount <= 0)
            return 0;
        if (tier.IsTerminal)
            return depositAmount;
        return Math.Min(depositAmount, Math.Round(depositAmount * tier.PercentOfDeposit / 100m, 0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Trễ nhận: bậc theo số ngày trễ kể từ giờ nhận. No-show khi tới bậc terminal hoặc đã hết thời gian thuê
    /// mà chưa nhận (đơn ngắn hơn mốc no-show vẫn mất trọn cọc).
    /// </summary>
    public static RentalPickupStatus GetPickupStatus(
        RentalPolicySnapshot policy,
        decimal depositAmount,
        DateTime rentalStartUtc,
        DateTime rentalEndUtc,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var days = FullDaysLate(rentalStartUtc, nowUtc);
        var tier = FindTier(policy.PickupTiers, days);
        var isNoShow = (tier?.IsTerminal ?? false) || (nowUtc >= rentalEndUtc && nowUtc > rentalStartUtc);
        var fee = isNoShow ? depositAmount : TierFee(depositAmount, tier);
        return new RentalPickupStatus(days, tier, isNoShow, fee);
    }

    /// <summary>
    /// Báo giá huỷ đơn đã thanh toán, chưa nhận đồ.
    /// Trong 24h đầu kể từ giờ nhận (kể cả trước đó): miễn phí, trừ khi đã dùng hết lượt huỷ miễn phí.
    /// Sau đó áp bậc trễ nhận; tới mốc no-show thì không cho khách tự huỷ nữa.
    /// </summary>
    public static RentalCancellationQuote QuoteCancellation(
        RentalPolicySnapshot policy,
        decimal amountPaid,
        decimal depositAmount,
        DateTime rentalStartUtc,
        DateTime rentalEndUtc,
        DateTime nowUtc,
        int freeCancellationsInWindow)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var pickup = GetPickupStatus(policy, depositAmount, rentalStartUtc, rentalEndUtc, nowUtc);
        if (pickup.IsNoShow)
        {
            return new RentalCancellationQuote(
                false, RentalCancellationTypes.NoShow, pickup.DaysLate, pickup.Fee,
                Math.Max(0, amountPaid - pickup.Fee), pickup.Tier, freeCancellationsInWindow,
                "Đơn đã quá hạn nhận đồ và được xử lý như khách không đến");
        }

        string type;
        decimal fee;
        if (pickup.DaysLate == 0)
        {
            if (freeCancellationsInWindow >= policy.FreeCancelLimit)
            {
                type = RentalCancellationTypes.OverLimitCancel;
                fee = Math.Min(depositAmount, Math.Round(depositAmount * policy.OverLimitCancelFeePercent / 100m, 0, MidpointRounding.AwayFromZero));
            }
            else
            {
                type = RentalCancellationTypes.FreeCancel;
                fee = 0;
            }
        }
        else
        {
            type = RentalCancellationTypes.LateCancel;
            fee = pickup.Fee;
        }

        fee = Math.Min(fee, amountPaid);
        return new RentalCancellationQuote(
            true, type, pickup.DaysLate, fee, Math.Max(0, amountPaid - fee), pickup.Tier, freeCancellationsInWindow, null);
    }

    /// <summary>Trễ trả: bậc phạt tính từ hạn trả; nghi mất khi trễ từ <see cref="RentalPolicySnapshot.SuspectedLossAfterDays"/> ngày.</summary>
    public static RentalReturnStatus GetReturnStatus(
        RentalPolicySnapshot policy,
        decimal depositAmount,
        DateTime rentalEndUtc,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var days = FullDaysLate(rentalEndUtc, nowUtc);
        var tier = FindTier(policy.ReturnTiers, days);
        return new RentalReturnStatus(days, tier, TierFee(depositAmount, tier), days >= policy.SuspectedLossAfterDays);
    }

    /// <summary>Khách còn được gia hạn khi chưa trễ quá <see cref="RentalPolicySnapshot.ExtensionMaxDaysLate"/> ngày đủ.</summary>
    public static bool CanExtend(RentalPolicySnapshot policy, DateTime rentalEndUtc, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return FullDaysLate(rentalEndUtc, nowUtc) <= policy.ExtensionMaxDaysLate;
    }
}
