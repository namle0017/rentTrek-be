using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Danh sách / chi tiết đơn, override participant, thanh toán tay, hủy đơn và đối soát tiền chuyển sai.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminOrderService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    INotificationService notificationService)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminOrderService
{
    private static AdminOrderResponse ToResponse(AdminOrderRow x) => new(
        x.OrderId, x.OrderCode, x.OrderType,
        new AdminCustomerResponse(x.CustomerUserId, x.CustomerUserCode, x.CustomerFullName, x.CustomerEmail, x.CustomerPhone),
        x.OrderType == OrderTypes.TripBooking ? x.TourName + " · " + x.ParticipantCount + " người"
            : "Thuê thiết bị · " + x.RentalItemQuantity + " món",
        x.TotalAmount, x.Status, x.PaymentStatus, x.HoldExpiresAt, x.ReconciliationDeadlineAt, x.HasPendingRefund, x.CreatedAt);

    public async Task<PagedResponse<AdminOrderResponse>> GetOrdersAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        CheckPaging(request);
        var page = await _unitOfWork.Orders.GetAdminPagedAsync(new AdminOrderFilter
        {
            ScopeUserId = OrderScope,
            OrderType = request.OrderType,
            Status = request.Status,
            PaymentStatus = request.PaymentStatus,
            Tab = request.Tab,
            Search = request.Search,
            SortBy = request.SortBy,
            IsDescending = IsDescending(request),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
        return ToPaged(page, request, ToResponse);
    }

    public async Task<AdminOrderDetailResponse> GetOrderAsync(string orderCode, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetAdminRowAsync(orderCode, OrderScope, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng");
        var participants = (await _unitOfWork.Orders.GetParticipantsAsync(order.OrderId, cancellationToken))
            .Select(x => new AdminParticipantResponse(x.ParticipantId, x.FullName, x.Email, x.Phone, x.DateOfBirth,
                x.EmergencyContactName, x.EmergencyContactPhone, x.Status)).ToList();
        var payments = (await _unitOfWork.Orders.GetPaymentsAsync(order.OrderId, cancellationToken))
            .Select(x => new AdminPaymentResponse(x.PaymentId, x.PaymentType, x.Purpose, x.Amount, x.PaymentMethod,
                x.ProviderTransactionId, x.Status, x.CreatedAt, x.ProcessedAt)).ToList();
        var lines = (await _unitOfWork.Orders.GetRentalLinesAsync(order.OrderId, cancellationToken))
            .Select(x => new AdminRentalLineResponse(x.RentalOrderItemId, x.Bundle != null ? x.Bundle.Name : x.ProductVariant!.Name,
                x.Quantity, x.BillableDays, x.DailyPrice, x.LineAmount,
                x.RentalBundleItemSnapshots.Select(s => new AdminRentalComponentResponse(s.ProductVariantId, s.ProductVariant.Name, s.QuantityPerBundle)).ToList(),
                x.AssetAssignments.Select(a => new AdminAssetResponse(a.AssetAssignmentId, a.EquipmentAsset.AssetCode, a.Status,
                    a.ReservedFrom, a.ReservedUntil, a.ReturnedAt)).ToList())).ToList();
        var promotions = (await _unitOfWork.Orders.GetAppliedPromotionsAsync(order.OrderId, cancellationToken))
            .Select(x => new AdminPromotionUsageResponse(x.AppliedPromotionId, x.OrderId, order.OrderCode, x.DiscountAmount, x.GiftQuantity, x.Status, x.AppliedAt)).ToList();
        var audit = (await _unitOfWork.AuditLogs.GetForOrderAsync(order.OrderId, cancellationToken)).Select(ToAuditResponse).ToList();
        var rental = await _unitOfWork.Orders.GetRentalWithHandlerAsync(order.OrderId, cancellationToken);
        return new(ToResponse(order), participants, payments, lines, promotions, audit,
            rental == null ? null : new(rental.RentalStartAt, rental.RentalEndAt), rental?.HandledByUserId, rental?.HandledByUser?.FullName);
    }

    public async Task OverrideParticipantAsync(string orderCode, Guid participantId, ParticipantOverrideRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason.Trim();
        var fullName = request.FullName.Trim();
        await AtomicAsync("order:" + orderCode, async () =>
        {
            var order = await _unitOfWork.Orders.GetAdminRowAsync(orderCode, OrderScope, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng");
            var participant = await _unitOfWork.Orders.GetParticipantForUpdateAsync(order.OrderId, participantId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy hành khách");
            Check(order.Status is "PendingPayment" or "Confirmed" && participant.Trip.StartAt > _timeProvider.GetUtcNow(), "status", "Chỉ sửa hành khách trước khi khởi hành");
            var before = new { participant.FullName, participant.Phone, participant.DateOfBirth, participant.EmergencyContactName, participant.EmergencyContactPhone };
            participant.FullName = fullName; participant.Phone = request.Phone?.Trim(); participant.DateOfBirth = request.DateOfBirth;
            participant.EmergencyContactName = request.EmergencyContactName?.Trim(); participant.EmergencyContactPhone = request.EmergencyContactPhone?.Trim(); participant.UpdatedAt = _timeProvider.GetUtcNow();
            await AuditAsync("Update", "BookingParticipant", participantId, new { orderId = order.OrderId, before, after = request, reason }, cancellationToken);
        }, cancellationToken);
    }

    public async Task ManualPaymentAsync(string orderCode, ManualPaymentRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var reason = request.Reason.Trim();
        var reference = request.ManualReference.Trim();
        await AtomicAsync("order:" + orderCode, async () =>
        {
            await _unitOfWork.AdminOperations.AcquireLockAsync("manual-reference:" + reference, cancellationToken);
            var order = await _unitOfWork.Orders.GetAdminForUpdateAsync(orderCode, OrderScope, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng");
            var duplicate = await _unitOfWork.Orders.GetManualCollectionAsync(reference, cancellationToken);
            if (duplicate != null)
            {
                if (duplicate.OrderId == order.OrderId && duplicate.Amount == request.Amount) return;
                throw new ConflictException("Mã tham chiếu đã được ghi nhận với số tiền khác");
            }
            Check(order.Status is "PendingPayment" or "Confirmed", "status", "Đơn hàng không thể nhận thanh toán thủ công");
            Check(order.HoldExpiresAt == null || order.HoldExpiresAt > _timeProvider.GetUtcNow() || order.Status == OrderStatuses.Confirmed, "status", "Đơn đã hết hạn giữ chỗ");
            var collected = order.Payments.Where(x => x.PaymentType == "Collection" && x.Status == PaymentStatuses.Succeeded && x.Purpose != "AdditionalCharge").Sum(x => x.Amount);
            Check(collected + request.Amount <= order.TotalAmount, "amount", "Số tiền vượt quá số tiền còn phải thanh toán");
            await _unitOfWork.Payments.AddAsync(new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = order.OrderId,
                Amount = request.Amount,
                PaymentType = "Collection",
                PaymentMethod = "Manual",
                ProviderTransactionId = reference,
                Purpose = order.OrderType == OrderTypes.TripBooking ? "TourFee" : "RentalFee",
                Status = PaymentStatuses.Succeeded,
                CreatedAt = _timeProvider.GetUtcNow(),
                ProcessedAt = _timeProvider.GetUtcNow()
            }, cancellationToken);
            order.PaymentStatus = collected + request.Amount == order.TotalAmount ? "Paid" : "PartiallyPaid";
            if (order.PaymentStatus == OrderPaymentStatuses.Paid)
            {
                order.Status = OrderStatuses.Confirmed; order.HoldExpiresAt = null; order.ReconciliationDeadlineAt = null;
                var promotions = await _unitOfWork.Orders.GetAppliedPromotionsAsync(order.OrderId, cancellationToken);
                foreach (var promotion in promotions.Where(x => x.Status == AppliedPromotionStatuses.Reserved))
                { promotion.Status = AppliedPromotionStatuses.Applied; promotion.AppliedAt = Now; }

                await notificationService.QueueAsync(
                    order.CustomerUserId,
                    NotificationTypes.OrderConfirmed,
                    "Đơn hàng đã được xác nhận",
                    BuildOrderNotificationContent(order, "đã được xác nhận"),
                    "Order",
                    order.OrderId,
                    $"OrderConfirmed:{order.OrderId}",
                    cancellationToken);
            }
            order.UpdatedAt = _timeProvider.GetUtcNow();
            await AuditAsync("ManualPayment", "Order", order.OrderId, new { request.Amount, request.PaymentMethod, reference, reason }, cancellationToken);
        }, cancellationToken);
    }

    public async Task CancelOrderAsync(string orderCode, ReasonRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var reason = request.Reason.Trim();
        await AtomicAsync("order:" + orderCode, async () =>
        {
            var order = await _unitOfWork.Orders.GetAdminForUpdateAsync(orderCode, OrderScope, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng");
            if (order.Status == OrderStatuses.Cancelled) return;
            Check(order.Status is "PendingPayment" or "Confirmed", "status", "Chỉ hủy đơn trước khi thực hiện dịch vụ");
            var assignments = await _unitOfWork.Orders.GetAssignmentsForUpdateAsync(order.OrderId, cancellationToken);
            Check(assignments.All(x => x.Status != AssignmentStatuses.Issued), "status", "Còn thiết bị đang được sử dụng");
            foreach (var assignment in assignments.Where(x => x.Status == AssignmentStatuses.Reserved)) assignment.Status = AssignmentStatuses.Cancelled;
            var participants = await _unitOfWork.Orders.GetParticipantsAsync(order.OrderId, cancellationToken);
            foreach (var participant in participants) { participant.Status = BookingParticipantStatuses.Cancelled; participant.UpdatedAt = _timeProvider.GetUtcNow(); }
            var invitations = await _unitOfWork.Orders.GetOpenInvitationsForUpdateAsync(order.OrderId, cancellationToken);
            foreach (var invitation in invitations) invitation.RevokedAt = _timeProvider.GetUtcNow();
            var promotions = await _unitOfWork.Orders.GetAppliedPromotionsAsync(order.OrderId, cancellationToken);
            foreach (var promotion in promotions.Where(x => x.Status is AppliedPromotionStatuses.Reserved or AppliedPromotionStatuses.Applied))
                promotion.Status = AppliedPromotionStatuses.Reversed;
            await AddFullRefundsAsync(order, cancellationToken);
            order.Status = OrderStatuses.Cancelled; order.CancelReason = reason; order.CancelledAt = _timeProvider.GetUtcNow();
            order.HoldExpiresAt = null; order.ReconciliationDeadlineAt = null; order.UpdatedAt = _timeProvider.GetUtcNow();
            await notificationService.QueueAsync(
                order.CustomerUserId,
                NotificationTypes.OrderCancelled,
                "Đơn hàng đã bị hủy",
                BuildOrderNotificationContent(order, "đã bị hủy"),
                "Order",
                order.OrderId,
                $"OrderCancelled:{order.OrderId}",
                cancellationToken);
            await AuditAsync("Cancel", "Order", order.OrderId, new { reason }, cancellationToken);
        }, cancellationToken);
    }

    public Task ReconcileAcceptAsync(Guid orderId, CancellationToken cancellationToken)
        => ReconcileAsync(orderId, "ReconcileAccept", async (order, received) =>
        {
            var holdValid = order.Status == OrderStatuses.PendingPayment
                && (order.HoldExpiresAt == null || order.HoldExpiresAt > _timeProvider.GetUtcNow());
            if (order.Status != OrderStatuses.Confirmed && !holdValid)
                throw new ConflictException(
                    "Đơn đã hết hạn giữ chỗ hoặc đã đóng, không thể xác nhận. Vui lòng dùng Hoàn toàn bộ (reconcile-refund).",
                    ErrorCodes.OrderNotReconcilable);

            var receivedAmount = received.Sum(x => x.Amount);
            var paid = order.Payments.Where(x => x.PaymentType == "Collection" && x.Status == PaymentStatuses.Succeeded && x.Purpose != "AdditionalCharge").Sum(x => x.Amount)
                + receivedAmount;
            if (paid < order.TotalAmount)
                throw new ConflictException(
                    "Tổng tiền đã nhận chưa đủ giá trị đơn. Vui lòng dùng Hoàn toàn bộ (reconcile-refund).",
                    ErrorCodes.OrderNotReconcilable);

            MarkReceived(received);
            var wasPending = order.Status == OrderStatuses.PendingPayment;
            order.Status = OrderStatuses.Confirmed;
            order.PaymentStatus = OrderPaymentStatuses.Paid;
            var promotions = await _unitOfWork.Orders.GetAppliedPromotionsAsync(order.OrderId, cancellationToken);
            foreach (var promotion in promotions.Where(x => x.Status == AppliedPromotionStatuses.Reserved))
            { promotion.Status = AppliedPromotionStatuses.Applied; promotion.AppliedAt = Now; }

            // Tiền thừa: sinh lệnh hoàn chờ duyệt, gắn với lần chuyển vừa nhận
            var excess = paid - order.TotalAmount;
            if (excess > 0)
            {
                var source = received[^1];
                await AddRefundAsync(order, source, excess, cancellationToken);
            }

            if (wasPending)
                await notificationService.QueueAsync(
                    order.CustomerUserId,
                    NotificationTypes.OrderConfirmed,
                    "Đơn hàng đã được xác nhận",
                    BuildOrderNotificationContent(order, "đã được xác nhận"),
                    "Order",
                    order.OrderId,
                    $"OrderConfirmed:{order.OrderId}",
                    cancellationToken);
            return new { receivedAmount, paid, excess };
        }, cancellationToken);

    public Task ReconcileRefundAsync(Guid orderId, CancellationToken cancellationToken)
        => ReconcileAsync(orderId, "ReconcileRefund", async (order, received) =>
        {
            if (order.Status == OrderStatuses.Confirmed)
                throw new ConflictException(
                    "Đơn đã xác nhận: dùng Chấp nhận khoản tiền (reconcile-accept) để hoàn phần thừa, hoặc hủy đơn để hoàn toàn bộ.",
                    ErrorCodes.OrderNotReconcilable);

            MarkReceived(received);
            var previousStatus = order.Status;
            if (order.Status == OrderStatuses.PendingPayment)
            {
                // Giải phóng giữ chỗ như khi hủy đơn, rồi đóng đơn ở Expired
                var assignments = await _unitOfWork.Orders.GetAssignmentsForUpdateAsync(order.OrderId, cancellationToken);
                foreach (var assignment in assignments.Where(x => x.Status == AssignmentStatuses.Reserved)) assignment.Status = AssignmentStatuses.Cancelled;
                var participants = await _unitOfWork.Orders.GetParticipantsAsync(order.OrderId, cancellationToken);
                foreach (var participant in participants) { participant.Status = BookingParticipantStatuses.Cancelled; participant.UpdatedAt = _timeProvider.GetUtcNow(); }
                var invitations = await _unitOfWork.Orders.GetOpenInvitationsForUpdateAsync(order.OrderId, cancellationToken);
                foreach (var invitation in invitations) invitation.RevokedAt = _timeProvider.GetUtcNow();
                var promotions = await _unitOfWork.Orders.GetAppliedPromotionsAsync(order.OrderId, cancellationToken);
                foreach (var promotion in promotions.Where(x => x.Status is AppliedPromotionStatuses.Reserved or AppliedPromotionStatuses.Applied))
                    promotion.Status = AppliedPromotionStatuses.Reversed;
                order.Status = OrderStatuses.Expired;
            }

            await AddFullRefundsAsync(order, cancellationToken);
            // Thoát NeedsReconciliation để mark-manual / hồ sơ hoàn tiền xử lý tiếp; mark-manual sẽ chuyển sang Refunded
            var collected = order.Payments.Where(x => x.PaymentType == "Collection" && x.Status == PaymentStatuses.Succeeded && x.Purpose != "AdditionalCharge").Sum(x => x.Amount);
            order.PaymentStatus = collected >= order.TotalAmount ? OrderPaymentStatuses.Paid : "PartiallyPaid";
            return new { receivedAmount = received.Sum(x => x.Amount), previousStatus, status = order.Status };
        }, cancellationToken);

    /// <summary>
    /// Khung chung cho đối soát: khoá thanh toán của đơn trước rồi mới khoá đơn (cùng thứ tự với webhook SePay để không deadlock),
    /// chỉ cho đơn đang NeedsReconciliation, chạy <paramref name="resolve"/> với các khoản tiền đã nhận (Collection Pending có mã giao dịch) và ghi audit.
    /// </summary>
    private async Task ReconcileAsync(
        Guid orderId,
        string action,
        Func<Order, List<Payment>, Task<object>> resolve,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        var orderCode = await _unitOfWork.Orders.GetOrderCodeAsync(orderId, OrderScope, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng");
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await _unitOfWork.AdminOperations.AcquireLockAsync("order:" + orderCode, cancellationToken);
            var order = await _unitOfWork.Orders.GetAdminForUpdateAsync(orderCode, OrderScope, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng");
            if (order.PaymentStatus != OrderPaymentStatuses.NeedsReconciliation)
                throw new ConflictException("Đơn không ở trạng thái cần đối soát hoặc đã được xử lý", ErrorCodes.OrderNotReconcilable);

            var received = order.Payments
                .Where(x => x.PaymentType == "Collection" && x.Status == PaymentStatuses.Pending && x.ProviderTransactionId != null)
                .OrderBy(x => x.CreatedAt)
                .ToList();
            var details = await resolve(order, received);
            order.HoldExpiresAt = null; order.ReconciliationDeadlineAt = null; order.UpdatedAt = _timeProvider.GetUtcNow();
            await AuditAsync(action, "Order", order.OrderId, details, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Ghi nhận các khoản tiền chờ đối soát là đã thu (Succeeded).</summary>
    private void MarkReceived(List<Payment> received)
    {
        foreach (var payment in received) { payment.Status = PaymentStatuses.Succeeded; payment.ProcessedAt = _timeProvider.GetUtcNow(); }
    }

    /// <summary>Sinh lệnh hoàn PendingApproval cho phần còn lại (chưa hoàn / chưa chờ hoàn) của mọi khoản thu thành công.</summary>
    private async Task AddFullRefundsAsync(Order order, CancellationToken cancellationToken)
    {
        foreach (var payment in order.Payments.Where(x => x.PaymentType == "Collection" && x.Status == PaymentStatuses.Succeeded).ToList())
        {
            var pendingOrPaidRefund = order.Payments.Where(x => x.OriginalPaymentId == payment.PaymentId && x.PaymentType == "Refund" && x.Status != PaymentStatuses.Cancelled).Sum(x => x.Amount);
            if (payment.Amount > pendingOrPaidRefund)
                await AddRefundAsync(order, payment, payment.Amount - pendingOrPaidRefund, cancellationToken);
        }
    }

    /// <summary>Thêm một lệnh hoàn PendingApproval (lưu số dương) gắn với khoản thu gốc.</summary>
    private Task AddRefundAsync(Order order, Payment original, decimal amount, CancellationToken cancellationToken)
        => _unitOfWork.Payments.AddAsync(new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            OriginalPaymentId = original.PaymentId,
            PaymentType = "Refund",
            Purpose = original.Purpose,
            PaymentMethod = original.PaymentMethod,
            Amount = amount,
            Status = PaymentStatuses.PendingApproval,
            CreatedAt = _timeProvider.GetUtcNow()
        }, cancellationToken);

    private static string BuildOrderNotificationContent(Order order, string statusText)
    {
        var tourName = order.TripBookingOrder?.Trip?.Tour?.Name;
        if (!string.IsNullOrWhiteSpace(tourName))
            return $"Đơn {order.OrderCode} cho chuyến đi {tourName.Trim()} {statusText}.";

        return order.OrderType == OrderTypes.Rental
            ? $"Đơn thuê thiết bị {order.OrderCode} {statusText}."
            : $"Đơn {order.OrderCode} {statusText}.";
    }
}
