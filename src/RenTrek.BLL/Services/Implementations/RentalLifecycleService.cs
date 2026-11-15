using System.Globalization;
using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <inheritdoc cref="IRentalLifecycleService"/>
public sealed class RentalLifecycleService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IRentalPolicyService rentalPolicyService,
    IRentalCancellationService cancellationService,
    IRentalNoticeDispatcher noticeDispatcher,
    IRentalOrderService rentalOrderService) : IRentalLifecycleService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly int[] BankInfoReminderDays = [3, 7];
    private const int ExtensionHoldMinutesFallback = 15;

    public async Task<IReadOnlyList<RentalLifecycleWorkItem>> CollectWorkAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        var policy = await rentalPolicyService.GetActiveSnapshotAsync(cancellationToken);
        var items = new List<RentalLifecycleWorkItem>();

        // Mọi đơn đã tới giờ nhận mà chưa giao: bậc phí tính chính xác theo policy riêng của đơn lúc xử lý
        foreach (var id in await unitOfWork.RentalOrders.GetPickupOverdueOrderIdsAsync(nowUtc, cancellationToken))
            items.Add(new(RentalLifecycleWorkKind.PickupOverdue, id));

        // Nhắc trước 24h và khi tới hạn (cửa sổ 1h trước đến 24h sau)
        foreach (var id in await unitOfWork.RentalOrders.GetReturnDueOrderIdsAsync(nowUtc.AddHours(-1), nowUtc.AddHours(24), cancellationToken))
            items.Add(new(RentalLifecycleWorkKind.ReturnReminder, id));

        foreach (var id in await unitOfWork.RentalOrders.GetReturnOverdueOrderIdsAsync(nowUtc, cancellationToken))
            items.Add(new(RentalLifecycleWorkKind.ReturnOverdue, id));

        foreach (var id in await unitOfWork.RentalOrders.GetAwaitingReceiptConfirmOrderIdsAsync(nowUtc.AddHours(-policy.ReceiptAutoConfirmHours), cancellationToken))
            items.Add(new(RentalLifecycleWorkKind.AutoConfirmReceipt, id));

        foreach (var refund in await unitOfWork.RefundCases.GetAwaitingBankInfoAsync(now.AddDays(-BankInfoReminderDays[0]), cancellationToken))
            items.Add(new(RentalLifecycleWorkKind.BankInfoReminder, refund.RefundCaseId));

        var holdMinutes = await GetHoldMinutesAsync(cancellationToken);
        foreach (var adjustment in await unitOfWork.RentalOrders.GetStalePendingAdjustmentsAsync(nowUtc.AddMinutes(-holdMinutes), cancellationToken))
            items.Add(new(RentalLifecycleWorkKind.ExpirePendingExtension, adjustment.RentalOrderId));

        return items;
    }

    public Task ProcessAsync(RentalLifecycleWorkItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind switch
        {
            RentalLifecycleWorkKind.PickupOverdue => ProcessPickupOverdueAsync(item.Id, cancellationToken),
            RentalLifecycleWorkKind.ReturnReminder => ProcessReturnReminderAsync(item.Id, cancellationToken),
            RentalLifecycleWorkKind.ReturnOverdue => ProcessReturnOverdueAsync(item.Id, cancellationToken),
            RentalLifecycleWorkKind.AutoConfirmReceipt => AutoConfirmReceiptAsync(item.Id, cancellationToken),
            RentalLifecycleWorkKind.BankInfoReminder => RemindBankInfoAsync(item.Id, cancellationToken),
            RentalLifecycleWorkKind.ExpirePendingExtension => ExpirePendingExtensionAsync(item.Id, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    public async Task<RentalOrderDetailResponse> ConfirmReceiptAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken);
            if (order is null || order.CustomerUserId != userId)
                throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);

            var rental = order.RentalOrder!;
            if (rental.CustomerReceivedConfirmedAt is null)
            {
                if (rental.HandedOverAt is null || order.Status != OrderStatuses.InFulfillment)
                    throw new ConflictException("Đơn chưa được giao nên chưa thể xác nhận đã nhận", ErrorCodes.RentalReceiptNotConfirmable);

                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                rental.CustomerReceivedConfirmedAt = nowUtc;
                rental.ReceiptConfirmSource = RentalReceiptConfirmSources.Customer;
                await AuditAsync(userId, "RentalReceiptConfirmed", orderId, new { source = RentalReceiptConfirmSources.Customer }, nowUtc, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await rentalOrderService.GetByIdAsync(userId, orderId, cancellationToken);
    }

    // ---------- Trễ nhận ----------

    private async Task ProcessPickupOverdueAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var goNoShow = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken);
            if (order is null || order.Status != OrderStatuses.Confirmed || order.PaymentStatus != OrderPaymentStatuses.Paid
                || RentalOrderOps.IsHandedOver(order))
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return;
            }

            var rental = order.RentalOrder!;
            var policy = await rentalPolicyService.GetSnapshotAsync(rental.RentalPolicyId, cancellationToken);
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            var start = RentalPeriodCalculator.ToUtc(rental.RentalStartAt);
            var pickup = RentalPenaltyCalculator.GetPickupStatus(policy, rental.DepositAmount, start, RentalPeriodCalculator.ToUtc(rental.RentalEndAt), nowUtc);

            if (pickup.IsNoShow)
            {
                goNoShow = true;
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }
            else if (pickup.Tier is { } tier && pickup.Fee > 0)
            {
                var (newCharge, changed) = RentalOrderOps.UpsertTieredCharge(
                    rental,
                    RentalChargeTypes.LatePickup,
                    pickup.Fee,
                    tier.MinDaysLate,
                    $"Trễ nhận đồ {pickup.DaysLate} ngày ({tier.PercentOfDeposit:0.##}% cọc)",
                    null,
                    nowUtc);
                if (newCharge is not null)
                    await unitOfWork.RentalCharges.AddAsync(newCharge, cancellationToken);

                if (changed)
                {
                    var cancelQuote = RentalPenaltyCalculator.QuoteCancellation(
                        policy, RentalOrderOps.AmountPaid(order), rental.DepositAmount, start,
                        RentalPeriodCalculator.ToUtc(rental.RentalEndAt), nowUtc, 0);
                    var noShowTier = policy.PickupTiers.FirstOrDefault(t => t.IsTerminal);
                    await AuditAsync(null, "RentalLatePickupFee", orderId, new { pickup.DaysLate, tier.PercentOfDeposit, pickup.Fee }, nowUtc, cancellationToken);
                    await noticeDispatcher.QueueAsync(
                        order.CustomerUserId,
                        order.OrderCode,
                        order.OrderId,
                        NotificationTypes.RentalLateFee,
                        $"Bạn đã trễ nhận đồ {pickup.DaysLate} ngày",
                        $"Đơn {order.OrderCode} bị tính phí {pickup.Fee:N0}đ ({tier.PercentOfDeposit:0.##}% cọc). Bạn có thể đến nhận hoặc huỷ đơn để được hoàn {cancelQuote.RefundAmount:N0}đ.",
                        $"RentalLateFee:{order.OrderId}:pickup:{tier.MinDaysLate}",
                        new RentalEmailNotice(
                            "Bạn đã trễ nhận đồ",
                            $"Đơn thuê đã trễ nhận {pickup.DaysLate} ngày",
                            [
                                ("Giờ nhận đã hẹn", FormatVn(start)),
                                ("Phí trễ hiện tại", $"<strong>{pickup.Fee:N0} đ</strong> ({tier.PercentOfDeposit:0.##}% cọc)"),
                                ("Nếu huỷ bây giờ, hoàn lại", $"{cancelQuote.RefundAmount:N0} đ"),
                                ("Mất toàn bộ cọc nếu chưa nhận sau", noShowTier is null ? "—" : $"{noShowTier.MinDaysLate} ngày")
                            ],
                            "Vui lòng đến cửa hàng nhận đồ (thời gian trả không thay đổi) hoặc mở chi tiết đơn để huỷ. Nếu không đến, đơn sẽ tự huỷ và bạn mất toàn bộ tiền cọc."),
                        cancellationToken);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
            }
            else
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }
        }
        catch
        {
            noticeDispatcher.Discard();
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        await noticeDispatcher.FlushAsync(cancellationToken);
        if (goNoShow)
            await cancellationService.ProcessNoShowAsync(orderId, null, null, false, cancellationToken);
    }

    // ---------- Nhắc trả ----------

    private async Task ProcessReturnReminderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var detail = await unitOfWork.RentalOrders.GetDetailAsync(orderId, cancellationToken);
        if (detail is null || detail.Status != OrderStatuses.InFulfillment)
            return;

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var end = RentalPeriodCalculator.ToUtc(detail.RentalEndAt);
        var isDue = nowUtc >= end;
        var stage = isDue ? "due" : "24h";
        var policy = await rentalPolicyService.GetSnapshotAsync(detail.RentalPolicyId, cancellationToken);
        var firstPenalty = policy.ReturnTiers.FirstOrDefault();

        var queued = await noticeDispatcher.QueueAsync(
            detail.CustomerUserId,
            detail.OrderCode,
            detail.OrderId,
            NotificationTypes.RentalReturnReminder,
            isDue ? "Đã đến hạn trả đồ" : "Sắp đến hạn trả đồ",
            $"Đơn {detail.OrderCode} cần trả trước {FormatVn(end)}. Cần thêm thời gian? Hãy gia hạn trong chi tiết đơn.",
            $"RentalReturnReminder:{detail.OrderId}:{end.Ticks}:{stage}",
            new RentalEmailNotice(
                isDue ? "Đã đến hạn trả đồ" : "Nhắc hạn trả đồ",
                isDue ? "Đơn thuê đã đến hạn trả" : "Đơn thuê sắp đến hạn trả",
                [
                    ("Hạn trả", $"<strong>{FormatVn(end)}</strong>"),
                    ("Ân hạn", $"{policy.ReturnGraceHours} giờ"),
                    ("Gia hạn được tới", $"hết {policy.ExtensionMaxDaysLate} ngày sau hạn trả"),
                    ("Phạt trễ bắt đầu từ", firstPenalty is null ? "—" : $"{firstPenalty.MinDaysLate} ngày trễ (cộng tiền thuê mỗi ngày trễ)")
                ],
                "Nếu cần dùng thêm, vui lòng gia hạn và thanh toán trong chi tiết đơn. Trả trễ sẽ bị tính tiền thuê theo ngày và phạt theo % tiền cọc."),
            cancellationToken);
        if (queued)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await noticeDispatcher.FlushAsync(cancellationToken);
        }
    }

    // ---------- Trả trễ / nghi mất ----------

    private async Task ProcessReturnOverdueAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken);
            if (order is null || order.Status != OrderStatuses.InFulfillment
                || !RentalOrderOps.Assignments(order).Any(a => a.Status == AssignmentStatuses.Issued))
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return;
            }

            var rental = order.RentalOrder!;
            var policy = await rentalPolicyService.GetSnapshotAsync(rental.RentalPolicyId, cancellationToken);
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            var end = RentalPeriodCalculator.ToUtc(rental.RentalEndAt);
            var status = RentalPenaltyCalculator.GetReturnStatus(policy, rental.DepositAmount, end, nowUtc);
            if (status.DaysLate < 1)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return;
            }

            if (status.Tier is { } tier && status.PenaltyFee > 0)
            {
                var (newCharge, changed) = RentalOrderOps.UpsertTieredCharge(
                    rental,
                    RentalChargeTypes.LateReturnPenalty,
                    status.PenaltyFee,
                    tier.MinDaysLate,
                    $"Phạt trả trễ {status.DaysLate} ngày ({tier.PercentOfDeposit:0.##}% cọc)",
                    null,
                    nowUtc);
                if (newCharge is not null)
                    await unitOfWork.RentalCharges.AddAsync(newCharge, cancellationToken);
                if (changed)
                    await AuditAsync(null, "RentalLateReturnPenalty", orderId, new { status.DaysLate, tier.PercentOfDeposit, status.PenaltyFee }, nowUtc, cancellationToken);
            }

            var canStillExtend = RentalPenaltyCalculator.CanExtend(policy, end, nowUtc);
            await noticeDispatcher.QueueAsync(
                order.CustomerUserId,
                order.OrderCode,
                order.OrderId,
                NotificationTypes.RentalReturnOverdue,
                $"Bạn đã trả đồ trễ {status.DaysLate} ngày",
                status.PenaltyFee > 0
                    ? $"Đơn {order.OrderCode} đang bị phạt {status.PenaltyFee:N0}đ cộng tiền thuê các ngày trễ. Vui lòng trả đồ sớm nhất."
                    : $"Đơn {order.OrderCode} đã quá hạn trả. {(canStillExtend ? "Bạn vẫn có thể gia hạn trong chi tiết đơn." : "Vui lòng trả đồ sớm nhất.")}",
                $"RentalReturnOverdue:{order.OrderId}:{end.Ticks}:{status.DaysLate}",
                new RentalEmailNotice(
                    "Đơn thuê đã quá hạn trả",
                    $"Bạn đã trễ hạn trả {status.DaysLate} ngày",
                    [
                        ("Hạn trả", FormatVn(end)),
                        ("Phạt trễ hiện tại", $"<strong>{status.PenaltyFee:N0} đ</strong>"),
                        ("Tiền thuê ngày trễ", "Tính khi nhận lại đồ"),
                        ("Coi như mất đồ sau", $"{policy.SuspectedLossAfterDays} ngày trễ")
                    ],
                    canStillExtend
                        ? "Bạn vẫn có thể gia hạn (thanh toán ngay) trong chi tiết đơn để tránh bị phạt."
                        : $"Vui lòng mang đồ đến trả ngay. Quá {policy.SuspectedLossAfterDays} ngày, thiết bị bị coi là mất và bạn phải bồi thường giá trị thiết bị."),
                cancellationToken);

            if (status.IsSuspectedLoss
                && await unitOfWork.RentalReports.GetOpenAsync(order.OrderId, RentalReportTypes.SuspectedLoss, cancellationToken) is null)
            {
                var issuedCodes = RentalOrderOps.Assignments(order)
                    .Where(a => a.Status == AssignmentStatuses.Issued)
                    .Select(a => a.EquipmentAsset.AssetCode)
                    .ToList();
                await unitOfWork.RentalReports.AddAsync(new RentalReport
                {
                    RentalReportId = Guid.NewGuid(),
                    RentalOrderId = order.OrderId,
                    ReportType = RentalReportTypes.SuspectedLoss,
                    Status = RentalReportStatuses.Submitted,
                    Amount = 0,
                    Note = $"Quá hạn trả {status.DaysLate} ngày, chưa nhận lại: {string.Join(", ", issuedCodes)}. Cần staff liên hệ và xác nhận mất.",
                    CreatedByUserId = null,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                }, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            noticeDispatcher.Discard();
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        await noticeDispatcher.FlushAsync(cancellationToken);
    }

    // ---------- Tự xác nhận đã nhận ----------

    private async Task AutoConfirmReceiptAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var rental = await unitOfWork.RentalOrders.GetByIdAsync(orderId, cancellationToken);
        if (rental is null || rental.HandedOverAt is null || rental.CustomerReceivedConfirmedAt is not null)
            return;

        var policy = await rentalPolicyService.GetSnapshotAsync(rental.RentalPolicyId, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (RentalPeriodCalculator.ToUtc(rental.HandedOverAt.Value).AddHours(policy.ReceiptAutoConfirmHours) > nowUtc)
            return;

        rental.CustomerReceivedConfirmedAt = nowUtc;
        rental.ReceiptConfirmSource = RentalReceiptConfirmSources.Auto;
        await AuditAsync(null, "RentalReceiptConfirmed", orderId, new { source = RentalReceiptConfirmSources.Auto }, nowUtc, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------- Nhắc nhập STK ----------

    private async Task RemindBankInfoAsync(Guid refundCaseId, CancellationToken cancellationToken)
    {
        var refund = await unitOfWork.RefundCases.GetDetailAsync(refundCaseId, cancellationToken);
        if (refund is null || refund.Status != RefundCaseStatuses.AwaitingBankInfo || refund.BankInfoRequestedAt is null)
            return;

        var days = (timeProvider.GetUtcNow() - refund.BankInfoRequestedAt.Value).TotalDays;
        var milestone = BankInfoReminderDays.Where(d => days >= d).DefaultIfEmpty(0).Max();
        if (milestone == 0)
            return;

        var queued = await noticeDispatcher.QueueAsync(
            refund.CustomerUserId,
            refund.Order.OrderCode,
            refund.OrderId,
            NotificationTypes.RefundBankInfoRequired,
            "Nhắc nhập tài khoản nhận hoàn tiền",
            $"Đơn {refund.Order.OrderCode} còn {refund.Amount:N0}đ chưa hoàn vì bạn chưa nhập số tài khoản.",
            $"RefundBankInfoReminder:{refund.RefundCaseId}:{milestone}",
            new RentalEmailNotice(
                "Nhắc nhập tài khoản nhận hoàn tiền",
                "Bạn có khoản tiền đang chờ hoàn",
                [("Số tiền chờ hoàn", $"<strong>{refund.Amount:N0} đ</strong>")],
                "Vui lòng đăng nhập renTrek, mở chi tiết đơn và nhập số tài khoản ngân hàng để kế toán chuyển tiền cho bạn."),
            cancellationToken);
        if (queued)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await noticeDispatcher.FlushAsync(cancellationToken);
        }
    }

    // ---------- Gia hạn quá hạn thanh toán ----------

    private async Task ExpirePendingExtensionAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var holdMinutes = await GetHoldMinutesAsync(cancellationToken);
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken);
            var adjustment = await unitOfWork.RentalOrders.GetPendingAdjustmentAsync(orderId, cancellationToken);
            if (order is null || adjustment is null
                || adjustment.CreatedAt.AddMinutes(holdMinutes) > timeProvider.GetUtcNow().UtcDateTime)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return;
            }

            var adjustmentId = adjustment.RentalOrderAdjustmentId;

            var payment = order.Payments.FirstOrDefault(p => p.PaymentId == adjustment.PaymentId);
            if (payment is { Status: PaymentStatuses.Succeeded })
            {
                // Tiền đã vào nhưng chưa áp (hiếm): để webhook / admin xử lý, không huỷ
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return;
            }

            if (payment is not null)
                payment.Status = PaymentStatuses.Cancelled;
            adjustment.Status = RentalAdjustmentStatuses.Cancelled;

            // Trả lại giữ chỗ về hạn trả hiện tại
            var end = order.RentalOrder!.RentalEndAt;
            foreach (var assignment in RentalOrderOps.Assignments(order)
                         .Where(a => a.Status is AssignmentStatuses.Reserved or AssignmentStatuses.Issued && a.ReservedUntil > end))
                assignment.ReservedUntil = end;

            await AuditAsync(null, "RentalExtensionExpired", order.OrderId, new { adjustmentId, adjustment.ExtraAmount }, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<int> GetHoldMinutesAsync(CancellationToken cancellationToken)
    {
        var value = await unitOfWork.SystemConfigurations.GetValueAsync(RentalConfigKeys.HoldMinutes, cancellationToken);
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes
            : ExtensionHoldMinutesFallback;
    }

    private static string FormatVn(DateTime utc)
        => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(VietnamOffset).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    private Task AuditAsync(Guid? actorUserId, string action, Guid orderId, object details, DateTime nowUtc, CancellationToken cancellationToken)
        => unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = "Order",
            EntityId = orderId,
            Details = JsonSerializer.Serialize(details),
            CreatedAt = nowUtc
        }, cancellationToken);
}
