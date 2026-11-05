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

/// <summary>Danh sách / chi tiết đơn, override participant, thanh toán tay và hủy đơn.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminOrderService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
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
            foreach (var payment in order.Payments.Where(x => x.PaymentType == "Collection" && x.Status == PaymentStatuses.Succeeded).ToList())
            {
                var pendingOrPaidRefund = order.Payments.Where(x => x.OriginalPaymentId == payment.PaymentId && x.PaymentType == "Refund" && x.Status != PaymentStatuses.Cancelled).Sum(x => x.Amount);
                if (payment.Amount > pendingOrPaidRefund)
                    await _unitOfWork.Payments.AddAsync(new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        OrderId = order.OrderId,
                        OriginalPaymentId = payment.PaymentId,
                        PaymentType = "Refund",
                        Purpose = payment.Purpose,
                        PaymentMethod = payment.PaymentMethod,
                        Amount = payment.Amount - pendingOrPaidRefund,
                        Status = PaymentStatuses.PendingApproval,
                        CreatedAt = _timeProvider.GetUtcNow()
                    }, cancellationToken);
            }
            order.Status = OrderStatuses.Cancelled; order.CancelReason = reason; order.CancelledAt = _timeProvider.GetUtcNow();
            order.HoldExpiresAt = null; order.ReconciliationDeadlineAt = null; order.UpdatedAt = _timeProvider.GetUtcNow();
            await AuditAsync("Cancel", "Order", order.OrderId, new { reason }, cancellationToken);
        }, cancellationToken);
    }
}
