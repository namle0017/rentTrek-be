namespace RenTrek.BLL.Helpers;

/// <summary>
/// Công thức phí trả trễ và quyết toán cọc của đơn thuê.
/// Phí trễ là đề xuất [D] (PRD Q-04): mỗi 24 giờ trễ bắt đầu sau thời gian ân hạn tính 1 ngày giá thuê đã chốt.
/// Quyết toán cọc theo PRD §8.7 / contract 03.1 phụ lục C.
/// </summary>
public static class RentalFulfillmentCalculator
{
    /// <summary>Số ngày trễ: 0 nếu trả trong hạn + ân hạn; ngược lại làm tròn lên theo 24 giờ tính từ hạn trả.</summary>
    public static int CalculateLateDays(DateTime rentalEndUtc, DateTime returnedAtUtc, decimal graceHours)
    {
        var lateHours = (decimal)(returnedAtUtc - rentalEndUtc).TotalHours;
        if (lateHours <= graceHours)
            return 0;

        return (int)Math.Ceiling(lateHours / 24m);
    }

    /// <summary>
    /// Quy số thiết bị trả trễ về số "đơn vị thuê" của dòng: dòng combo 2 món × 1 combo = 1 đơn vị,
    /// trễ 1 trong 2 món vẫn tính 1 đơn vị (làm tròn lên).
    /// </summary>
    public static int CalculateLateUnits(int lateAssetCount, int lineQuantity, int lineAssetCount)
    {
        if (lateAssetCount <= 0 || lineAssetCount <= 0)
            return 0;

        return (int)Math.Ceiling(lateAssetCount * (decimal)lineQuantity / lineAssetCount);
    }

    public static decimal CalculateLateFee(int lateDays, decimal dailyPrice, int lateUnits)
        => Math.Max(0, lateDays) * dailyPrice * Math.Max(0, lateUnits);

    /// <summary>Bù phí từ cọc trước; phần cọc dư hoàn khách, phần phí vượt cọc phải thu thêm.</summary>
    public static (decimal DepositToRefund, decimal AdditionalAmountDue) Settle(decimal depositHeld, decimal validCharges)
        => (Math.Max(0, depositHeld - validCharges), Math.Max(0, validCharges - depositHeld));
}
