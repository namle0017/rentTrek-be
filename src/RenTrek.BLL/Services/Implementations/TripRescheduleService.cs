using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Đổi lịch booking sang chuyến khác cùng tour theo điều khoản đổi lịch của chính sách khách đã chấp nhận.
/// Chênh lệch ≤ 0 đổi ngay (âm thì mở hồ sơ hoàn phần chênh); &gt; 0 giữ chỗ chuyến mới 15 phút chờ chuyển khoản.
/// </summary>
public sealed class TripRescheduleService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IRefundCaseService refundCaseService,
    IRentalRefundOpener refundOpener,
    ITripGroupingService tripGroupingService,
    INotificationService notificationService) : ITripRescheduleService, ITripReschedulePaymentHandler
{
    private const string StatusPendingPayment = "PendingPayment";
    private const string StatusCompleted = "Completed";
    private const string StatusExpired = "Expired";
    private const string StatusFailed = "Failed";
    private static readonly TimeSpan PaymentHold = TimeSpan.FromMinutes(15);

    public async Task<RescheduleOptionsResponse> GetOptionsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOwnedAsync(orderId, cancellationToken);
        var utcNow = timeProvider.GetUtcNow();
        var booking = order.TripBookingOrder;
        var policy = await unitOfWork.RefundCases.GetPolicyWithRulesAsync(order.RefundPolicyId, cancellationToken);
        var blockedReason = await GetBlockedReasonAsync(order, policy, utcNow, cancellationToken);
        var companyCancelled = IsCompanyCancelled(order);

        var response = new RescheduleOptionsResponse
        {
            CanReschedule = blockedReason is null,
            BlockedReason = blockedReason,
            CompanyCancelled = companyCancelled,
            Fee = Fee(policy, companyCancelled),
            RemainingTimes = companyCancelled ? 1 : Math.Max(0, (policy?.MaxReschedules ?? 0) - booking.RescheduleCount),
            Deadline = !companyCancelled && policy?.RescheduleMinHoursBefore is { } minHours ? booking.Trip.StartAt.AddHours(-minHours) : null,
            Pending = (await unitOfWork.TripReschedules.GetPendingByOrderAsync(orderId, cancellationToken))
                .Where(x => x.HoldExpiresAt > utcNow)
                .Select(x => ToResult(x, null))
                .FirstOrDefault()
        };
        if (blockedReason is not null)
            return response;

        var trips = await unitOfWork.TripReschedules.GetOpenSiblingTripsAsync(booking.Trip.TourId, booking.TripId, utcNow, cancellationToken);
        foreach (var trip in trips)
        {
            var available = trip.TotalSlots - await unitOfWork.TripBookings.GetReservedParticipantCountAsync(trip.TripId, utcNow, cancellationToken);
            if (available < booking.ParticipantCount)
                continue;
            response.Trips.Add(new RescheduleOptionsResponse.TripOption
            {
                TripId = trip.TripId,
                StartAt = trip.StartAt,
                EndAt = trip.EndAt,
                PricePerPerson = trip.PricePerPerson,
                AvailableSlots = available,
                PriceDiff = PriceDiff(booking, trip, response.Fee, companyCancelled)
            });
        }

        return response;
    }

    public async Task<RescheduleResultResponse> RescheduleAsync(
        Guid orderId,
        RescheduleTripBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.TripBookings.AcquireBookingLockAsync(orderId, cancellationToken);
            var order = await GetOwnedAsync(orderId, cancellationToken);
            var booking = order.TripBookingOrder;
            var policy = await unitOfWork.RefundCases.GetPolicyWithRulesAsync(order.RefundPolicyId, cancellationToken);
            var blockedReason = await GetBlockedReasonAsync(order, policy, utcNow, cancellationToken);
            if (blockedReason is not null)
                throw new ConflictException(blockedReason, ErrorCodes.OrderNotCancellable);

            // Bấm lại cùng chuyến khi đang chờ trả tiền: trả lại khoản đang chờ; đổi ý chuyến khác: bỏ khoản cũ
            foreach (var pending in await unitOfWork.TripReschedules.GetPendingByOrderAsync(orderId, cancellationToken))
            {
                if (pending.ToTripId == request.ToTripId && pending.HoldExpiresAt > utcNow)
                {
                    await unitOfWork.CommitAsync(cancellationToken);
                    return ToResult(pending, null);
                }

                await ExpireAsync(pending, cancellationToken);
            }

            if (request.ToTripId == booking.TripId)
                throw new ConflictException("Booking đang ở chuyến này", ErrorCodes.ValidationError);

            var (fromTrip, toTrip) = await LockTripsAsync(booking.TripId, request.ToTripId, cancellationToken);
            var unavailable = await GetTargetUnavailableReasonAsync(booking, fromTrip, toTrip, 0, utcNow, cancellationToken);
            if (unavailable is not null)
                throw new ConflictException(unavailable, ErrorCodes.ValidationError);

            var companyCancelled = IsCompanyCancelled(order);
            var fee = Fee(policy, companyCancelled);
            var reschedule = new TripBookingReschedule
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                FromTripId = fromTrip.TripId,
                ToTripId = toTrip.TripId,
                ParticipantCount = booking.ParticipantCount,
                PriceDiff = PriceDiff(booking, toTrip, fee, companyCancelled),
                Fee = fee,
                CreatedAt = utcNow
            };
            await unitOfWork.TripReschedules.AddAsync(reschedule, cancellationToken);

            RefundCaseSummaryResponse? refundCase = null;
            if (reschedule.PriceDiff > 0)
            {
                var payment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    OrderId = orderId,
                    PaymentType = "Collection",
                    Amount = reschedule.PriceDiff,
                    PaymentMethod = "BankTransfer",
                    Status = PaymentStatuses.Pending,
                    CreatedAt = utcNow,
                    Purpose = PaymentPurposes.TripReschedule
                };
                await unitOfWork.Payments.AddAsync(payment, cancellationToken);
                reschedule.PaymentId = payment.PaymentId;
                reschedule.Status = StatusPendingPayment;
                reschedule.HoldExpiresAt = utcNow.Add(PaymentHold);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                refundCase = await CompleteAsync(order, reschedule, fromTrip, toTrip, actorUserId, utcNow, cancellationToken);
            }

            await unitOfWork.CommitAsync(cancellationToken);
            return ToResult(reschedule, refundCase);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task ApplySucceededPaymentAsync(Payment payment, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var reschedule = await unitOfWork.TripReschedules.GetByPaymentIdAsync(payment.PaymentId, cancellationToken);
        var order = await unitOfWork.TripBookings.GetBookingAsync(payment.OrderId, cancellationToken);
        if (reschedule is null || order?.TripBookingOrder is null)
            return;

        var booking = order.TripBookingOrder;
        var (fromTrip, toTrip) = await LockTripsAsync(booking.TripId, reschedule.ToTripId, cancellationToken);
        // Chỗ của chính lần đổi này đang được giữ thì không tính là đã bị chiếm
        var ownHold = reschedule.Status == StatusPendingPayment && reschedule.HoldExpiresAt > utcNow ? reschedule.ParticipantCount : 0;
        var failure = reschedule.Status != StatusPendingPayment || booking.TripId != reschedule.FromTripId
            ? "Yêu cầu đổi lịch không còn hiệu lực"
            : await GetTargetUnavailableReasonAsync(booking, fromTrip, toTrip, ownHold, utcNow, cancellationToken);

        if (failure is null)
        {
            await CompleteAsync(order, reschedule, fromTrip, toTrip, order.CustomerUserId, utcNow, cancellationToken);
            return;
        }

        // Khách đã chuyển tiền nhưng không đổi được: giữ chuyến cũ, hoàn lại khoản vừa trả
        if (reschedule.Status == StatusPendingPayment)
            reschedule.Status = StatusFailed;
        await refundOpener.OpenRentalRefundAsync(order, payment.Amount, RefundCaseSources.TripReschedule, null, null, utcNow, cancellationToken);
        await notificationService.QueueAsync(
            order.CustomerUserId,
            NotificationTypes.TripScheduleChanged,
            "Đổi lịch không thành công",
            $"Đơn {order.OrderCode}: {failure}. Khoản {payment.Amount:N0}đ sẽ được hoàn, vui lòng nhập tài khoản nhận hoàn.",
            "Order",
            order.OrderId,
            $"trip-reschedule-failed:{reschedule.Id:N}",
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<RefundCaseSummaryResponse?> CompleteAsync(
        Order order,
        TripBookingReschedule reschedule,
        Trip fromTrip,
        Trip toTrip,
        Guid actorUserId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var booking = order.TripBookingOrder;
        // Phải xét trước khi đổi booking.Trip sang chuyến mới
        var companyCancelled = IsCompanyCancelled(order);
        if (companyCancelled)
            await ReviveCancelledOrderAsync(order, utcNow, cancellationToken);
        else
            booking.RescheduleCount++;
        booking.TripId = toTrip.TripId;
        booking.Trip = toTrip;
        booking.UnitPrice = toTrip.PricePerPerson;
        // Xếp nhóm lại ở chuyến mới
        foreach (var participant in booking.Participants)
        {
            participant.TripId = toTrip.TripId;
            participant.TripGroupId = null;
            participant.UpdatedAt = utcNow;
        }

        // Tổng tiền đơn = số đã thu ròng, dùng làm trần khi tính hoàn lúc hủy sau này
        order.TotalAmount += reschedule.PriceDiff;
        order.SubtotalAmount += reschedule.PriceDiff;
        order.UpdatedAt = utcNow;
        foreach (var trip in new[] { fromTrip, toTrip })
        {
            trip.ProvidedItemsNeedsRecalc = true;
            trip.RowVersion++;
            trip.UpdatedAt = utcNow;
        }

        reschedule.Status = StatusCompleted;
        reschedule.CompletedAt = utcNow;

        await unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = "RescheduleTripBooking",
            EntityType = "TripBookingOrder",
            EntityId = order.OrderId,
            Details = JsonSerializer.Serialize(new
            {
                fromTripId = fromTrip.TripId,
                toTripId = toTrip.TripId,
                reschedule.PriceDiff,
                reschedule.Fee
            }),
            CreatedAt = utcNow.UtcDateTime
        }, cancellationToken);

        var refundCase = reschedule.PriceDiff < 0
            ? await refundOpener.OpenRentalRefundAsync(
                order, -reschedule.PriceDiff, RefundCaseSources.TripReschedule, null, null, utcNow, cancellationToken)
            : null;

        await notificationService.QueueAsync(
            order.CustomerUserId,
            NotificationTypes.TripScheduleChanged,
            "Đổi lịch thành công",
            $"Đơn {order.OrderCode} đã chuyển sang chuyến khởi hành {toTrip.StartAt.ToOffset(TimeSpan.FromHours(7)):HH:mm dd/MM/yyyy}.",
            "Order",
            order.OrderId,
            $"trip-reschedule:{reschedule.Id:N}",
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tripGroupingService.AutoAssignPaidBookingInTransactionAsync(order.OrderId, cancellationToken);
        return refundCase;
    }

    private async Task<Order> GetOwnedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await unitOfWork.TripBookings.GetOwnedBookingAsync(orderId, currentUser.RequiredUserId, cancellationToken);
        if (order?.TripBookingOrder is null || order.IsDeleted)
            throw new NotFoundException("Không tìm thấy booking", ErrorCodes.OrderNotFound);
        return order;
    }

    // Phase 3: công ty huỷ chuyến ⇒ đơn Cancelled kèm hồ sơ hoàn 100%; khách được chuyển sang chuyến khác thay vì nhận hoàn
    private static bool IsCompanyCancelled(Order order)
        => order.Status == OrderStatuses.Cancelled && order.TripBookingOrder.Trip.Status == TripStatuses.Cancelled;

    private static decimal Fee(RefundPolicy? policy, bool companyCancelled)
        => companyCancelled ? 0m : policy?.RescheduleFee ?? 0m;

    /// <summary>Đưa đơn bị huỷ do công ty về Confirmed, khôi phục người bị huỷ cùng lúc, huỷ hồ sơ hoàn 100%.</summary>
    private async Task ReviveCancelledOrderAsync(Order order, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        // AdminTripService huỷ participant cùng mốc với đơn; người bị bớt trước đó giữ nguyên Cancelled
        foreach (var participant in order.TripBookingOrder.Participants
                     .Where(p => p.Status == BookingParticipantStatuses.Cancelled && p.UpdatedAt == order.CancelledAt))
        {
            participant.Status = BookingParticipantStatuses.Pending;
            participant.UpdatedAt = utcNow;
        }

        order.Status = OrderStatuses.Confirmed;
        order.CancelledAt = null;
        order.CancelReason = null;

        var summary = await refundCaseService.GetSummaryByOrderAsync(order.OrderId, cancellationToken);
        var refund = summary is null ? null : await unitOfWork.RefundCases.GetForUpdateAsync(summary.RefundCaseId, cancellationToken);
        if (refund is null)
            return;
        refund.Status = RefundCaseStatuses.Cancelled;
        refund.RefundPayment.Status = PaymentStatuses.Cancelled;
        refund.RowVersion++;
        refund.UpdatedAt = utcNow;
        // Lưu trước để ux_refund_cases_open_order không chặn hồ sơ hoàn phần chênh (nếu có)
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> GetBlockedReasonAsync(
        Order order, RefundPolicy? policy, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        if (IsCompanyCancelled(order))
        {
            var cancelRefund = await refundCaseService.GetSummaryByOrderAsync(order.OrderId, cancellationToken);
            return cancelRefund is { Source: RefundCaseSources.TripCancellation, Status: RefundCaseStatuses.AwaitingBankInfo or RefundCaseStatuses.Requested }
                ? null
                : "Hồ sơ hoàn tiền của chuyến bị huỷ đã được xử lý, không thể chuyển chuyến";
        }
        if (order.Status != OrderStatuses.Confirmed || order.PaymentStatus != OrderPaymentStatuses.Paid)
            return "Chỉ đổi lịch khi đơn đã xác nhận và thanh toán";
        if (policy?.RescheduleMinHoursBefore is not { } minHours || policy.MaxReschedules <= 0)
            return "Chính sách của đơn không cho phép đổi lịch";
        if (order.TripBookingOrder.RescheduleCount >= policy.MaxReschedules)
            return "Đơn đã hết lượt đổi lịch";
        if (utcNow > order.TripBookingOrder.Trip.StartAt.AddHours(-minHours))
            return $"Chỉ đổi lịch trước giờ khởi hành ít nhất {minHours} giờ";
        var refund = await refundCaseService.GetSummaryByOrderAsync(order.OrderId, cancellationToken);
        if (refund?.Status is RefundCaseStatuses.AwaitingBankInfo or RefundCaseStatuses.Requested or RefundCaseStatuses.Approved)
            return "Đơn đang có yêu cầu hoàn tiền chưa xử lý xong";
        return null;
    }

    private async Task<string?> GetTargetUnavailableReasonAsync(
        TripBookingOrder booking, Trip fromTrip, Trip toTrip, int ownHold, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        if (toTrip.IsDeleted || toTrip.TourId != fromTrip.TourId)
            return "Chỉ đổi sang chuyến khác của cùng tour";
        if (toTrip.Status != TripStatuses.Open || toTrip.StartAt <= utcNow)
            return "Chuyến mới không còn nhận booking";
        var reserved = await unitOfWork.TripBookings.GetReservedParticipantCountAsync(toTrip.TripId, utcNow, cancellationToken) - ownHold;
        return reserved + booking.ParticipantCount > toTrip.TotalSlots ? "Chuyến mới không còn đủ chỗ" : null;
    }

    // Khoá 2 chuyến theo thứ tự tripId để 2 lần đổi ngược chiều không deadlock
    private async Task<(Trip From, Trip To)> LockTripsAsync(Guid fromTripId, Guid toTripId, CancellationToken cancellationToken)
    {
        foreach (var tripId in new[] { fromTripId, toTripId }.Order())
            await unitOfWork.TripGroups.AcquireTripLockAsync(tripId, cancellationToken);
        var from = await unitOfWork.TripBookings.GetTripForUpdateAsync(fromTripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");
        var to = await unitOfWork.TripBookings.GetTripForUpdateAsync(toTripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");
        return (from, to);
    }

    private async Task ExpireAsync(TripBookingReschedule pending, CancellationToken cancellationToken)
    {
        pending.Status = StatusExpired;
        if (pending.PaymentId is { } paymentId
            && await unitOfWork.Payments.GetByIdAsync(paymentId, cancellationToken) is { Status: PaymentStatuses.Pending } payment)
            payment.Status = PaymentStatuses.Cancelled;
    }

    private static decimal PriceDiff(TripBookingOrder booking, Trip toTrip, decimal fee, bool companyCancelled)
    {
        var diff = (toTrip.PricePerPerson - booking.UnitPrice) * booking.ParticipantCount + fee;
        // Công ty huỷ: khách không trả thêm, chỉ được hoàn nếu chuyến mới rẻ hơn
        return companyCancelled ? Math.Min(0m, diff) : diff;
    }

    private static RescheduleResultResponse ToResult(TripBookingReschedule reschedule, RefundCaseSummaryResponse? refundCase) => new()
    {
        RescheduleId = reschedule.Id,
        Status = reschedule.Status,
        ToTripId = reschedule.ToTripId,
        PriceDiff = reschedule.PriceDiff,
        PaymentId = reschedule.PaymentId,
        HoldExpiresAt = reschedule.HoldExpiresAt,
        RefundCase = refundCase
    };
}
