using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <inheritdoc cref="IRentalSettlementService"/>
public sealed class RentalSettlementService(
    IUnitOfWork unitOfWork,
    IRentalRefundOpener refundCaseService,
    IRentalNoticeDispatcher noticeDispatcher,
    IRentalExtensionApplier rentalOrderService,
    INotificationService notificationService) : IRentalSettlementService, IRentalPostPaymentService
{
    public string? GetBlockedReason(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Status != OrderStatuses.InFulfillment)
            return "Đơn không ở trạng thái đang thuê";
        var statuses = RentalOrderOps.Assignments(order).Select(a => a.Status).ToList();
        if (statuses.Contains(AssignmentStatuses.Reserved))
            return "Còn thiết bị chưa giao";
        if (statuses.Contains(AssignmentStatuses.Issued))
            return "Còn thiết bị chưa nhận trả";
        if (order.RentalOrder!.RentalCharges.Any(c => c.Status == RentalChargeStatuses.Draft))
            return "Còn phí nháp chưa duyệt hoặc huỷ";
        return null;
    }

    public async Task<RentalSettlement> SettleAsync(Order order, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (GetBlockedReason(order) is { } reason)
            throw new ConflictException(reason);
        if (await unitOfWork.RentalSettlements.GetByOrderAsync(order.OrderId, cancellationToken) is not null)
            throw new ConflictException("Đơn đã được quyết toán");

        var rental = order.RentalOrder!;
        var nowUtc = now.UtcDateTime;
        var amountPaid = RentalOrderOps.AmountPaid(order);
        var depositHeld = order.PaymentStatus == OrderPaymentStatuses.Paid ? Math.Min(rental.DepositAmount, amountPaid) : 0m;
        var charges = RentalOrderOps.AssessedCharges(rental);
        var (depositRefund, owed) = RentalFulfillmentCalculator.Settle(depositHeld, charges);

        var settlement = new RentalSettlement
        {
            RentalOrderId = order.OrderId,
            RentalFeePaid = Math.Max(0, amountPaid - depositHeld),
            DepositHeld = depositHeld,
            TotalCharges = charges,
            RentalFeeRefund = 0,
            DepositRefund = depositRefund,
            AmountOwed = owed,
            SettledByUserId = actorUserId,
            SettledAt = nowUtc
        };

        // Khoản thu đang chờ khác (vd. gia hạn chưa trả) không còn ý nghĩa sau khi đã nhận lại đồ
        foreach (var pending in order.Payments.Where(p => p.PaymentType == PaymentTypes.Collection && p.Status == PaymentStatuses.Pending))
            pending.Status = PaymentStatuses.Cancelled;

        if (depositRefund > 0)
        {
            var refund = await refundCaseService.OpenRentalRefundAsync(
                order, depositRefund, RefundCaseSources.RentalDepositReturn, null, null, now, cancellationToken);
            settlement.RefundCaseId = refund.RefundCaseId;
        }

        if (owed > 0)
        {
            var extra = new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = order.OrderId,
                PaymentType = PaymentTypes.Collection,
                Amount = owed,
                PaymentMethod = "BankTransfer",
                Status = PaymentStatuses.Pending,
                CreatedAt = now,
                Purpose = PaymentPurposes.AdditionalCharge
            };
            await unitOfWork.Payments.AddAsync(extra, cancellationToken);
            settlement.ExtraPaymentId = extra.PaymentId;
            order.Status = OrderStatuses.AwaitingSettlement;
            await unitOfWork.RentalReports.AddAsync(new RentalReport
            {
                RentalReportId = Guid.NewGuid(),
                RentalOrderId = order.OrderId,
                ReportType = RentalReportTypes.ExtraPayment,
                Status = RentalReportStatuses.Submitted,
                Amount = owed,
                Note = $"Phí phát sinh {charges:N0}đ vượt cọc {depositHeld:N0}đ. Khách cần thanh toán thêm {owed:N0}đ.",
                CreatedByUserId = actorUserId,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            }, cancellationToken);
        }
        else
        {
            order.Status = OrderStatuses.Completed;
        }

        order.UpdatedAt = now;
        await unitOfWork.RentalSettlements.AddAsync(settlement, cancellationToken);

        await noticeDispatcher.QueueAsync(
            order.CustomerUserId,
            order.OrderCode,
            order.OrderId,
            NotificationTypes.RentalSettlement,
            owed > 0 ? "Cần thanh toán thêm phí thuê đồ" : "Đã quyết toán cọc đơn thuê",
            owed > 0
                ? $"Đơn {order.OrderCode}: phí phát sinh vượt cọc, vui lòng thanh toán thêm {owed:N0}đ trong chi tiết đơn."
                : depositRefund > 0
                    ? $"Đơn {order.OrderCode}: còn {depositRefund:N0}đ cọc được hoàn. Vui lòng nhập số tài khoản để nhận tiền."
                    : $"Đơn {order.OrderCode} đã hoàn tất.",
            $"RentalSettlement:{order.OrderId}",
            new RentalEmailNotice(
                owed > 0 ? "Cần thanh toán thêm phí thuê đồ" : "Kết quả quyết toán cọc",
                owed > 0 ? "Phí phát sinh vượt tiền cọc" : "Đơn thuê đã được quyết toán",
                [
                    ("Tiền cọc", $"{depositHeld:N0} đ"),
                    ("Phí phát sinh", $"{charges:N0} đ"),
                    (owed > 0 ? "Cần trả thêm" : "Cọc được hoàn", $"<strong>{(owed > 0 ? owed : depositRefund):N0} đ</strong>")
                ],
                owed > 0
                    ? "Vui lòng mở chi tiết đơn để thanh toán qua mã QR. Tài khoản sẽ tạm khoá quyền thuê mới tới khi thanh toán xong."
                    : depositRefund > 0
                        ? "Vui lòng mở chi tiết đơn và nhập số tài khoản ngân hàng để kế toán hoàn cọc cho bạn."
                        : "Cảm ơn bạn đã thuê đồ tại renTrek."),
            cancellationToken);

        return settlement;
    }

    public async Task ApplySucceededPaymentAsync(Payment payment, Order order, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(order);

        if (payment.Purpose == PaymentPurposes.RentalExtension)
        {
            var adjustment = await unitOfWork.RentalOrders.GetAdjustmentByPaymentAsync(payment.PaymentId, cancellationToken);
            if (adjustment is null)
                return;
            var full = await unitOfWork.RentalOrders.GetForFulfillmentAsync(order.OrderId, cancellationToken) ?? order;
            await rentalOrderService.ApplyPaidExtensionAsync(full, adjustment, now.UtcDateTime, cancellationToken);
            return;
        }

        if (payment.Purpose == PaymentPurposes.AdditionalCharge)
        {
            var settlement = await unitOfWork.RentalSettlements.GetByExtraPaymentAsync(payment.PaymentId, cancellationToken);
            if (settlement is null || order.Status != OrderStatuses.AwaitingSettlement)
                return;

            order.Status = OrderStatuses.Completed;
            order.UpdatedAt = now;
            var report = await unitOfWork.RentalReports.GetOpenAsync(order.OrderId, RentalReportTypes.ExtraPayment, cancellationToken);
            if (report is not null)
            {
                report.Status = RentalReportStatuses.Resolved;
                report.ReviewNote = "Khách đã thanh toán khoản thu thêm";
                report.ReviewedAt = now.UtcDateTime;
                report.UpdatedAt = now.UtcDateTime;
                report.RowVersion++;
            }

            await notificationService.QueueAsync(
                order.CustomerUserId,
                NotificationTypes.RentalSettlement,
                "Đã nhận thanh toán phí phát sinh",
                $"Đơn {order.OrderCode} đã hoàn tất.",
                "Order",
                order.OrderId,
                $"RentalExtraPaid:{payment.PaymentId}",
                cancellationToken);
        }
    }
}
