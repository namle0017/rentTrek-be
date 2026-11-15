using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Thao tác dùng chung trên đồ thị đơn thuê đã tải có tracking (huỷ, no-show, phí theo bậc, quyết toán).
/// Không gọi DB; service tự SaveChanges.
/// </summary>
public static class RentalOrderOps
{
    /// <summary>Tiền khách đã trả cho đơn ở lần thanh toán chính (tiền thuê + cọc), không gồm gia hạn / phí vượt cọc.</summary>
    public static decimal AmountPaid(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return order.Payments
            .Where(p => p.PaymentType == PaymentTypes.Collection
                        && p.Status == PaymentStatuses.Succeeded
                        && !PaymentPurposes.AfterOrderPaid.Contains(p.Purpose))
            .Sum(p => p.Amount);
    }

    public static IEnumerable<AssetAssignment> Assignments(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return order.RentalOrder!.RentalOrderItems.SelectMany(i => i.AssetAssignments)
            .Concat(order.AppliedPromotions.SelectMany(p => p.AssetAssignments))
            .Distinct();
    }

    /// <summary>Đã giao ít nhất một thiết bị (hoặc đã ghi nhận giao).</summary>
    public static bool IsHandedOver(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return order.RentalOrder!.HandedOverAt is not null
               || Assignments(order).Any(a => a.Status is AssignmentStatuses.Issued or AssignmentStatuses.Returned or AssignmentStatuses.LostOrException);
    }

    /// <summary>Huỷ đơn chưa giao: nhả giữ chỗ, hoàn lượt khuyến mãi, huỷ khoản thu đang chờ.</summary>
    public static void CancelOrder(Order order, string cancellationType, string reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(order);
        foreach (var assignment in Assignments(order).Where(a => a.Status == AssignmentStatuses.Reserved))
            assignment.Status = AssignmentStatuses.Cancelled;
        foreach (var promotion in order.AppliedPromotions)
            promotion.Status = AppliedPromotionStatuses.Reversed;
        foreach (var payment in order.Payments.Where(p => p.PaymentType == PaymentTypes.Collection && p.Status == PaymentStatuses.Pending))
            payment.Status = PaymentStatuses.Cancelled;

        order.Status = OrderStatuses.Cancelled;
        order.CancelledAt = now;
        order.CancelReason = reason.Length > 500 ? reason[..500] : reason;
        order.HoldExpiresAt = null;
        order.UpdatedAt = now;
        order.RentalOrder!.CancellationType = cancellationType;
    }

    /// <summary>
    /// Ghi phí theo bậc (trễ nhận / phạt trả trễ / phí huỷ): mỗi loại chỉ một dòng hiệu lực và chỉ được nâng.
    /// Trả về charge mới tạo (để caller AddAsync) hoặc null nếu cập nhật dòng cũ / không đổi; <c>changed</c> cho biết có nâng bậc.
    /// </summary>
    public static (RentalCharge? NewCharge, bool Changed) UpsertTieredCharge(
        RentalOrder rental,
        string chargeType,
        decimal amount,
        int tierMinDays,
        string reason,
        Guid? actorUserId,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rental);
        if (amount <= 0)
            return (null, false);

        var existing = rental.RentalCharges.FirstOrDefault(c => c.ChargeType == chargeType && c.Status != RentalChargeStatuses.Voided);
        if (existing is not null)
        {
            if (existing.Amount >= amount && (existing.TierMinDays ?? 0) >= tierMinDays)
                return (null, false);

            existing.Amount = Math.Max(existing.Amount, amount);
            existing.TierMinDays = Math.Max(existing.TierMinDays ?? 0, tierMinDays);
            existing.Reason = Truncate(reason);
            return (null, true);
        }

        var charge = new RentalCharge
        {
            RentalChargeId = Guid.NewGuid(),
            RentalOrderId = rental.OrderId,
            ChargeType = chargeType,
            Amount = amount,
            Reason = Truncate(reason),
            Status = RentalChargeStatuses.Assessed,
            CreatedByUserId = actorUserId,
            TierMinDays = tierMinDays,
            CreatedAt = nowUtc
        };
        rental.RentalCharges.Add(charge);
        return (charge, true);
    }

    /// <summary>Tổng phí hợp lệ (Assessed) để trừ cọc.</summary>
    public static decimal AssessedCharges(RentalOrder rental)
    {
        ArgumentNullException.ThrowIfNull(rental);
        return rental.RentalCharges.Where(c => c.Status == RentalChargeStatuses.Assessed).Sum(c => c.Amount);
    }

    /// <summary>
    /// Kết quả quyết toán khi huỷ (chưa nhận đồ): hoàn = đã trả − phí; phí trừ cọc trước, phần cọc còn lại và toàn bộ tiền thuê được hoàn.
    /// </summary>
    public static RentalSettlement BuildCancellationSettlement(Order order, decimal amountPaid, decimal fee, Guid? actorUserId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(order);
        var deposit = Math.Min(order.RentalOrder!.DepositAmount, amountPaid);
        var rentalFeePaid = amountPaid - deposit;
        var cappedFee = Math.Min(fee, amountPaid);
        var depositRefund = Math.Max(0, deposit - cappedFee);
        var feeBeyondDeposit = Math.Max(0, cappedFee - deposit);
        return new RentalSettlement
        {
            RentalOrderId = order.OrderId,
            RentalFeePaid = rentalFeePaid,
            DepositHeld = deposit,
            TotalCharges = cappedFee,
            RentalFeeRefund = Math.Max(0, rentalFeePaid - feeBeyondDeposit),
            DepositRefund = depositRefund,
            AmountOwed = 0,
            SettledByUserId = actorUserId,
            SettledAt = nowUtc
        };
    }

    /// <summary>
    /// Giá trị đền bù của một thiết bị: dòng biến thể lấy giá trị đã chụp lúc đặt; dòng combo lấy giá trị biến thể của thiết bị đó.
    /// 0 = chưa có giá trị (staff / admin phải lập phí mất thủ công).
    /// </summary>
    public static decimal AssetReplacementValue(AssetAssignment assignment, IReadOnlyDictionary<Guid, decimal?> variantValues)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(variantValues);
        var item = assignment.RentalOrderItem;
        if (item?.ProductVariantId is not null && item.UnitReplacementValue > 0)
            return item.UnitReplacementValue;
        return variantValues.GetValueOrDefault(assignment.EquipmentAsset.ProductVariantId) ?? 0m;
    }

    private static string Truncate(string value) => value.Length > 500 ? value[..500] : value;
}
