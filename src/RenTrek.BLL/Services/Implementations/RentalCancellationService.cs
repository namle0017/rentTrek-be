using System.Globalization;
using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Huỷ đơn thuê đã thanh toán và no-show theo chính sách cọc:
/// trước giờ nhận + 24h miễn phí (giới hạn số lần / 30 ngày), trễ 1 ngày 5% cọc, 2 ngày 20% cọc, 3 ngày hoặc hết thời gian thuê
/// = no-show mất trọn cọc. Mọi số tiền do server tính; khách chỉ xác nhận số đã xem ở preview.
/// </summary>
public sealed class RentalCancellationService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IRentalPolicyService rentalPolicyService,
    IRentalRefundOpener refundCaseService,
    IRentalNoticeDispatcher noticeDispatcher,
    IRentalOrderService rentalOrderService) : IRentalCancellationService
{
    public async Task<RentalCancellationPreviewResponse> PreviewAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var detail = await unitOfWork.RentalOrders.GetOwnedDetailAsync(orderId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);

        var policy = await rentalPolicyService.GetSnapshotAsync(detail.RentalPolicyId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var amountPaid = detail.Payments
            .Where(p => p.PaymentType == PaymentTypes.Collection && p.Status == PaymentStatuses.Succeeded
                        && !PaymentPurposes.AfterOrderPaid.Contains(p.Purpose))
            .Sum(p => p.Amount);
        var freeCount = await CountFreeCancellationsAsync(userId, policy, now, cancellationToken);

        var eligible = detail.Status == OrderStatuses.Confirmed
                       && detail.PaymentStatus == OrderPaymentStatuses.Paid
                       && detail.HandedOverAt is null
                       && !detail.Assignments.Any(a => a.Status is AssignmentStatuses.Issued or AssignmentStatuses.Returned or AssignmentStatuses.LostOrException);

        var quote = RentalPenaltyCalculator.QuoteCancellation(
            policy,
            amountPaid,
            detail.DepositAmount,
            RentalPeriodCalculator.ToUtc(detail.RentalStartAt),
            RentalPeriodCalculator.ToUtc(detail.RentalEndAt),
            now.UtcDateTime,
            freeCount);

        var allowed = eligible && quote.Allowed;
        return new RentalCancellationPreviewResponse
        {
            OrderId = orderId,
            Allowed = allowed,
            BlockedReason = !eligible
                ? "Chỉ huỷ theo chính sách được với đơn đã thanh toán và chưa nhận đồ"
                : quote.BlockedReason,
            CancellationType = quote.CancellationType,
            DaysLate = quote.DaysLate,
            AmountPaid = amountPaid,
            DepositAmount = detail.DepositAmount,
            Fee = quote.Fee,
            RefundAmount = quote.RefundAmount,
            AppliedTierPercent = quote.CancellationType == RentalCancellationTypes.OverLimitCancel
                ? policy.OverLimitCancelFeePercent
                : quote.AppliedTier?.PercentOfDeposit,
            FreeCancellationsUsed = freeCount,
            FreeCancelLimit = policy.FreeCancelLimit,
            FreeCancelWindowDays = policy.FreeCancelWindowDays,
            RequiresBankAccount = quote.RefundAmount > 0,
            ConsentVersion = RefundBankCodes.ConsentVersion
        };
    }

    public async Task<RentalOrderDetailResponse> CancelPaidAsync(
        Guid userId,
        Guid orderId,
        CancelPaidRentalOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Reason?.Trim().Length > 500)
            throw new BadRequestException("Lý do huỷ tối đa 500 ký tự", ErrorCodes.ValidationError);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Cùng thứ tự khoá với webhook / huỷ chưa thanh toán: khoá thanh toán rồi khoá vận hành
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken);
            if (order is null || order.CustomerUserId != userId)
                throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);

            if (order.Status != OrderStatuses.Confirmed || order.PaymentStatus != OrderPaymentStatuses.Paid)
                throw new ConflictException("Chỉ huỷ theo chính sách được với đơn đã thanh toán", ErrorCodes.OrderNotCancellable);
            if (RentalOrderOps.IsHandedOver(order))
                throw new ConflictException("Không thể huỷ sau khi đã nhận thiết bị", ErrorCodes.OrderNotCancellable);

            var rental = order.RentalOrder!;
            var policy = await rentalPolicyService.GetSnapshotAsync(rental.RentalPolicyId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var amountPaid = RentalOrderOps.AmountPaid(order);
            var freeCount = await CountFreeCancellationsAsync(userId, policy, now, cancellationToken);
            var quote = RentalPenaltyCalculator.QuoteCancellation(
                policy,
                amountPaid,
                rental.DepositAmount,
                RentalPeriodCalculator.ToUtc(rental.RentalStartAt),
                RentalPeriodCalculator.ToUtc(rental.RentalEndAt),
                now.UtcDateTime,
                freeCount);

            if (!quote.Allowed)
                throw new ConflictException(quote.BlockedReason ?? "Đơn không thể huỷ", ErrorCodes.OrderNotCancellable);

            if (request.ExpectedRefundAmount != quote.RefundAmount)
            {
                throw new ConflictException(
                    "Số tiền hoàn đã thay đổi, vui lòng xem lại",
                    ErrorCodes.RefundAmountChanged,
                    new Dictionary<string, string[]> { ["refundAmount"] = [quote.RefundAmount.ToString("0", CultureInfo.InvariantCulture)] });
            }

            if (quote.RefundAmount > 0)
            {
                var errors = new Dictionary<string, string[]>();
                if (request.BankAccount is null)
                    errors["bankAccount"] = ["Cần cung cấp tài khoản nhận hoàn"];
                if (!request.ConsentAccepted || request.ConsentVersion != RefundBankCodes.ConsentVersion)
                    errors["consentAccepted"] = ["Bạn cần đồng ý xử lý dữ liệu ngân hàng"];
                if (errors.Count > 0)
                    throw new ValidationException(errors);
            }

            var nowUtc = now.UtcDateTime;
            if (quote.Fee > 0)
            {
                var (chargeType, reason) = quote.CancellationType == RentalCancellationTypes.LateCancel
                    ? (RentalChargeTypes.LatePickup, $"Huỷ khi đã trễ nhận {quote.DaysLate} ngày ({quote.AppliedTier?.PercentOfDeposit:0.##}% cọc)")
                    : (RentalChargeTypes.CancellationFee, $"Huỷ vượt {policy.FreeCancelLimit} lần miễn phí trong {policy.FreeCancelWindowDays} ngày ({policy.OverLimitCancelFeePercent:0.##}% cọc)");
                var (newCharge, _) = RentalOrderOps.UpsertTieredCharge(
                    rental, chargeType, quote.Fee, quote.DaysLate, reason, userId, nowUtc);
                if (newCharge is not null)
                    await unitOfWork.RentalCharges.AddAsync(newCharge, cancellationToken);
            }

            RentalOrderOps.CancelOrder(
                order,
                quote.CancellationType,
                string.IsNullOrWhiteSpace(request.Reason) ? "Khách huỷ đơn thuê đã thanh toán" : request.Reason.Trim(),
                now);

            var settlement = RentalOrderOps.BuildCancellationSettlement(order, amountPaid, quote.Fee, userId, nowUtc);
            if (quote.RefundAmount > 0)
            {
                var refund = await refundCaseService.OpenRentalRefundAsync(
                    order,
                    quote.RefundAmount,
                    RefundCaseSources.RentalCancellation,
                    request.BankAccount,
                    request.ConsentVersion,
                    now,
                    cancellationToken);
                settlement.RefundCaseId = refund.RefundCaseId;
            }

            await unitOfWork.RentalSettlements.AddAsync(settlement, cancellationToken);

            if (quote.CancellationType == RentalCancellationTypes.LateCancel)
            {
                await unitOfWork.RentalReports.AddAsync(new RentalReport
                {
                    RentalReportId = Guid.NewGuid(),
                    RentalOrderId = order.OrderId,
                    ReportType = RentalReportTypes.LateCancellation,
                    Status = RentalReportStatuses.Submitted,
                    Amount = quote.Fee,
                    Note = $"Khách huỷ khi đã trễ nhận {quote.DaysLate} ngày. Phí {quote.Fee:N0}đ, hoàn {quote.RefundAmount:N0}đ.",
                    CreatedByUserId = null,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                }, cancellationToken);
            }

            await AuditAsync(userId, "RentalCancelPaid", order.OrderId, new
            {
                quote.CancellationType,
                quote.DaysLate,
                amountPaid,
                quote.Fee,
                quote.RefundAmount
            }, nowUtc, cancellationToken);

            await noticeDispatcher.QueueAsync(
                userId,
                order.OrderCode,
                order.OrderId,
                NotificationTypes.OrderCancelled,
                "Đã huỷ đơn thuê",
                quote.RefundAmount > 0
                    ? $"Đơn {order.OrderCode} đã huỷ. Số tiền hoàn {quote.RefundAmount:N0}đ đang chờ kế toán chuyển khoản."
                    : $"Đơn {order.OrderCode} đã huỷ.",
                $"OrderCancelled:{order.OrderId}",
                new RentalEmailNotice(
                    "Xác nhận huỷ đơn thuê",
                    "Đơn thuê của bạn đã được huỷ",
                    [
                        ("Đã thanh toán", $"{amountPaid:N0} đ"),
                        ("Phí huỷ", $"{quote.Fee:N0} đ"),
                        ("Số tiền hoàn", $"<strong>{quote.RefundAmount:N0} đ</strong>")
                    ],
                    quote.RefundAmount > 0
                        ? "Tiền hoàn sẽ được kế toán chuyển vào tài khoản bạn đã cung cấp. Bạn có thể theo dõi tiến độ trong chi tiết đơn."
                        : "Đơn đã huỷ và không phát sinh tiền hoàn."),
                cancellationToken);

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
        return await rentalOrderService.GetByIdAsync(userId, orderId, cancellationToken);
    }

    public async Task<bool> ProcessNoShowAsync(
        Guid orderId,
        Guid? actorUserId,
        string? note,
        bool throwIfNotEligible,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);

            var rental = order.RentalOrder!;
            var policy = await rentalPolicyService.GetSnapshotAsync(rental.RentalPolicyId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var nowUtc = now.UtcDateTime;
            var pickup = RentalPenaltyCalculator.GetPickupStatus(
                policy,
                rental.DepositAmount,
                RentalPeriodCalculator.ToUtc(rental.RentalStartAt),
                RentalPeriodCalculator.ToUtc(rental.RentalEndAt),
                nowUtc);

            var eligible = order.Status == OrderStatuses.Confirmed
                           && order.PaymentStatus == OrderPaymentStatuses.Paid
                           && !RentalOrderOps.IsHandedOver(order)
                           && pickup.IsNoShow;
            if (!eligible)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                if (throwIfNotEligible)
                {
                    throw new ConflictException(
                        "Đơn chưa tới mốc khách không đến (hoặc đã giao / đã xử lý)",
                        ErrorCodes.RentalNoShowNotReached);
                }

                return false;
            }

            var amountPaid = RentalOrderOps.AmountPaid(order);
            var fee = Math.Min(rental.DepositAmount, amountPaid);
            var (newCharge, _) = RentalOrderOps.UpsertTieredCharge(
                rental,
                RentalChargeTypes.LatePickup,
                fee,
                pickup.DaysLate,
                $"Khách không đến nhận đồ (trễ {pickup.DaysLate} ngày): mất toàn bộ cọc",
                actorUserId,
                nowUtc);
            if (newCharge is not null)
                await unitOfWork.RentalCharges.AddAsync(newCharge, cancellationToken);

            RentalOrderOps.CancelOrder(order, RentalCancellationTypes.NoShow, "Khách không đến nhận đồ", now);

            var refundAmount = Math.Max(0, amountPaid - fee);
            var settlement = RentalOrderOps.BuildCancellationSettlement(order, amountPaid, fee, actorUserId, nowUtc);
            if (refundAmount > 0)
            {
                var refund = await refundCaseService.OpenRentalRefundAsync(
                    order, refundAmount, RefundCaseSources.RentalNoShow, null, null, now, cancellationToken);
                settlement.RefundCaseId = refund.RefundCaseId;
            }

            await unitOfWork.RentalSettlements.AddAsync(settlement, cancellationToken);
            await unitOfWork.RentalReports.AddAsync(new RentalReport
            {
                RentalReportId = Guid.NewGuid(),
                RentalOrderId = order.OrderId,
                ReportType = RentalReportTypes.NoShow,
                Status = RentalReportStatuses.Submitted,
                Amount = fee,
                Note = string.IsNullOrWhiteSpace(note)
                    ? $"Khách không đến nhận đồ sau {pickup.DaysLate} ngày. Mất cọc {fee:N0}đ, hoàn tiền thuê {refundAmount:N0}đ (chờ khách nhập STK)."
                    : note.Trim(),
                CreatedByUserId = actorUserId,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            }, cancellationToken);

            await AuditAsync(actorUserId, "RentalNoShow", order.OrderId, new { pickup.DaysLate, amountPaid, fee, refundAmount }, nowUtc, cancellationToken);

            await noticeDispatcher.QueueAsync(
                order.CustomerUserId,
                order.OrderCode,
                order.OrderId,
                NotificationTypes.RentalNoShow,
                "Đơn thuê bị huỷ do không đến nhận đồ",
                refundAmount > 0
                    ? $"Đơn {order.OrderCode} đã huỷ, cọc {fee:N0}đ bị giữ theo chính sách. Vui lòng nhập số tài khoản để nhận lại {refundAmount:N0}đ."
                    : $"Đơn {order.OrderCode} đã huỷ, cọc {fee:N0}đ bị giữ theo chính sách.",
                $"RentalNoShow:{order.OrderId}",
                new RentalEmailNotice(
                    "Đơn thuê bị huỷ do không đến nhận đồ",
                    "Bạn đã không đến nhận đồ đúng hạn",
                    [
                        ("Đã thanh toán", $"{amountPaid:N0} đ"),
                        ("Cọc bị giữ", $"{fee:N0} đ"),
                        ("Số tiền sẽ hoàn", $"<strong>{refundAmount:N0} đ</strong>")
                    ],
                    refundAmount > 0
                        ? "Vui lòng đăng nhập renTrek, mở chi tiết đơn và nhập số tài khoản ngân hàng để hệ thống hoàn phần tiền còn lại cho bạn."
                        : "Đơn không còn tiền hoàn."),
                cancellationToken);

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
        return true;
    }

    private Task<int> CountFreeCancellationsAsync(Guid userId, RentalPolicySnapshot policy, DateTimeOffset now, CancellationToken cancellationToken)
        => unitOfWork.RentalOrders.CountCancellationsAsync(
            userId,
            RentalCancellationTypes.CountedAsFreeCancel,
            now.AddDays(-policy.FreeCancelWindowDays),
            cancellationToken);

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
