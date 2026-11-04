using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Admin;

public sealed partial class AdminService
{
    private IQueryable<Order> ScopedOrders(bool tracking = false)
    {
        var query = tracking ? _unitOfWork.Orders.QueryTracking() : _unitOfWork.Orders.Query();
        return IsAdmin ? query : query.Where(x =>
            (x.OrderType == "Tour" && x.TripBookingOrder.Trip.ManagedByUserId == ActorId) ||
            (x.OrderType == "Rental" && x.RentalOrder != null && x.RentalOrder.HandledByUserId == ActorId));
    }

    private static readonly Expression<Func<Order, AdminOrderResponse>> OrderProjection = order => new(
        order.OrderId, order.OrderCode, order.OrderType,
        new AdminCustomerResponse(order.CustomerUserId, order.CustomerUser.Profile == null ? null : order.CustomerUser.Profile.UserCode,
            order.CustomerUser.FullName, order.CustomerUser.Email, order.CustomerUser.Phone),
        order.OrderType == "Tour" ? order.TripBookingOrder.Trip.Tour.Name + " · " + order.TripBookingOrder.ParticipantCount + " người"
            : "Thuê thiết bị · " + order.RentalOrder!.RentalOrderItems.Where(x => !x.IsDeleted).Sum(x => x.Quantity) + " món",
        order.TotalAmount, order.Status, order.PaymentStatus, order.HoldExpiresAt, order.ReconciliationDeadlineAt,
        order.Payments.Any(x => x.PaymentType == "Refund" && (x.Status == "PendingApproval" || x.Status == "Pending")), order.CreatedAt);

    public Task<PagedResponse<AdminOrderResponse>> GetOrdersAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = ScopedOrders();
        if (request.OrderType != null) query = query.Where(x => x.OrderType == request.OrderType);
        if (request.Status != null) query = query.Where(x => x.Status == request.Status);
        if (request.PaymentStatus != null) query = query.Where(x => x.PaymentStatus == request.PaymentStatus);
        if (request.Tab is "PendingPayment" or "Cancelled") query = query.Where(x => x.Status == request.Tab);
        if (request.Tab == "NeedsReconciliation") query = query.Where(x => x.PaymentStatus == "NeedsReconciliation");
        if (request.Tab == "PendingRefund") query = query.Where(x => x.Payments.Any(p => p.PaymentType == "Refund" && (p.Status == "PendingApproval" || p.Status == "Pending")));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.OrderCode.ToLower().Contains(search) || x.CustomerUser.Email.ToLower().Contains(search));
        }
        return PageAsync(query, request, OrderProjection, cancellationToken, "CreatedAt", "OrderCode", "TotalAmount", "Status", "HoldExpiresAt", "ReconciliationDeadlineAt");
    }

    private IQueryable<AssetAssignment> RentalAssignments(Guid orderId, bool tracking = false)
        => (tracking ? _unitOfWork.AssetAssignments.QueryTracking() : _unitOfWork.AssetAssignments.Query())
            .Where(x => (x.RentalOrderItem != null && x.RentalOrderItem.RentalOrderId == orderId)
                || (x.AppliedPromotion != null && x.AppliedPromotion.OrderId == orderId));

    public async Task<AdminOrderDetailResponse> GetOrderAsync(string orderCode, CancellationToken cancellationToken)
    {
        var order = await ScopedOrders().Where(x => x.OrderCode == orderCode).Select(OrderProjection)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Không tìm thấy đơn hàng");
        var participants = await _unitOfWork.BookingParticipants.Query().Where(x => !x.IsDeleted && x.BookingOrderId == order.OrderId)
            .Select(x => new AdminParticipantResponse(x.ParticipantId, x.FullName, x.Email, x.Phone, x.DateOfBirth,
                x.EmergencyContactName, x.EmergencyContactPhone, x.Status)).ToListAsync(cancellationToken);
        var payments = await _unitOfWork.Payments.Query().Where(x => x.OrderId == order.OrderId).OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminPaymentResponse(x.PaymentId, x.PaymentType, x.Purpose, x.Amount, x.PaymentMethod,
                x.ProviderTransactionId, x.Status, x.CreatedAt, x.ProcessedAt)).ToListAsync(cancellationToken);
        var lines = await _unitOfWork.RentalOrderItems.Query().Where(x => !x.IsDeleted && x.RentalOrderId == order.OrderId)
            .Select(x => new AdminRentalLineResponse(x.RentalOrderItemId, x.Bundle != null ? x.Bundle.Name : x.ProductVariant!.Name,
                x.Quantity, x.BillableDays, x.DailyPrice, x.LineAmount,
                x.RentalBundleItemSnapshots.Select(s => new AdminRentalComponentResponse(s.ProductVariantId, s.ProductVariant.Name, s.QuantityPerBundle)).ToList(),
                x.AssetAssignments.Select(a => new AdminAssetResponse(a.AssetAssignmentId, a.EquipmentAsset.AssetCode, a.Status,
                    a.ReservedFrom, a.ReservedUntil, a.ReturnedAt)).ToList())).ToListAsync(cancellationToken);
        var promotions = await _unitOfWork.AppliedPromotions.Query().Where(x => x.OrderId == order.OrderId)
            .Select(x => new AdminPromotionUsageResponse(x.AppliedPromotionId, x.OrderId, x.Order.OrderCode, x.DiscountAmount, x.GiftQuantity, x.Status, x.AppliedAt)).ToListAsync(cancellationToken);
        var audit = await _unitOfWork.AuditLogs.Query().Where(x => x.EntityId == order.OrderId ||
            (x.EntityType == "BookingParticipant" && _unitOfWork.BookingParticipants.Query().Any(p => p.BookingOrderId == order.OrderId && p.ParticipantId == x.EntityId)))
            .OrderByDescending(x => x.CreatedAt).Select(x => new AdminAuditResponse(x.AuditLogId, x.ActorUserId,
                x.ActorUser == null ? null : x.ActorUser.FullName, x.Action, x.EntityType, x.EntityId, AuditDetails(x.Details), x.CreatedAt)).ToListAsync(cancellationToken);
        var rental = await _unitOfWork.RentalOrders.Query().Where(x => x.OrderId == order.OrderId)
            .Select(x => new { x.RentalStartAt, x.RentalEndAt, x.HandledByUserId, HandlerFullName = x.HandledByUser == null ? null : x.HandledByUser.FullName }).SingleOrDefaultAsync(cancellationToken);
        return new(order, participants, payments, lines, promotions, audit,
            rental == null ? null : new(rental.RentalStartAt, rental.RentalEndAt), rental?.HandledByUserId, rental?.HandlerFullName);
    }

    public async Task OverrideParticipantAsync(string orderCode, Guid participantId, ParticipantOverrideRequest request, CancellationToken cancellationToken)
    {
        var reason = Required(request.Reason, "reason", 500);
        var fullName = Required(request.FullName, "fullName", 100);
        ValidatePhone(request.Phone, "phone"); ValidatePhone(request.EmergencyContactPhone, "emergencyContactPhone");
        Check(request.EmergencyContactName == null || request.EmergencyContactName.Trim().Length <= 200, "emergencyContactName", "Tên người liên hệ tối đa 200 ký tự");
        Check(request.DateOfBirth == null || request.DateOfBirth <= DateOnly.FromDateTime(Now), "dateOfBirth", "Ngày sinh không hợp lệ");
        await AtomicAsync("order:" + orderCode, async () =>
        {
            var order = await ScopedOrders().SingleOrDefaultAsync(x => x.OrderCode == orderCode, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng");
            var participant = await _unitOfWork.BookingParticipants.QueryTracking().Include(x => x.Trip)
                .SingleOrDefaultAsync(x => x.BookingOrderId == order.OrderId && x.ParticipantId == participantId && !x.IsDeleted, cancellationToken)
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
        var reason = Required(request.Reason, "reason", 500);
        var reference = Required(request.ManualReference, "manualReference", 100);
        Choice(request.PaymentMethod, "paymentMethod", "BankTransfer", "Cash");
        Check(request.Amount > 0 && request.Amount == decimal.Truncate(request.Amount), "amount", "Số tiền VND phải là số nguyên dương");
        await AtomicAsync("order:" + orderCode, async () =>
        {
            await _unitOfWork.AdminOperations.AcquireLockAsync("manual-reference:" + reference, cancellationToken);
            var order = await ScopedOrders(true).SingleOrDefaultAsync(x => x.OrderCode == orderCode, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng");
            var duplicate = await _unitOfWork.Payments.Query().SingleOrDefaultAsync(x => x.PaymentMethod == "Manual" && x.PaymentType == "Collection" && x.ProviderTransactionId == reference, cancellationToken);
            if (duplicate != null)
            {
                if (duplicate.OrderId == order.OrderId && duplicate.Amount == request.Amount) return;
                throw new ConflictException("Mã tham chiếu đã được ghi nhận với số tiền khác");
            }
            Check(order.Status is "PendingPayment" or "Confirmed", "status", "Đơn hàng không thể nhận thanh toán thủ công");
            Check(order.HoldExpiresAt == null || order.HoldExpiresAt > _timeProvider.GetUtcNow() || order.Status == "Confirmed", "status", "Đơn đã hết hạn giữ chỗ");
            var collected = await _unitOfWork.Payments.Query().Where(x => x.OrderId == order.OrderId && x.PaymentType == "Collection" && x.Status == "Succeeded" && x.Purpose != "AdditionalCharge").SumAsync(x => x.Amount, cancellationToken);
            Check(collected + request.Amount <= order.TotalAmount, "amount", "Số tiền vượt quá số tiền còn phải thanh toán");
            await _unitOfWork.Payments.AddAsync(new Payment { PaymentId = Guid.NewGuid(), OrderId = order.OrderId,
                Amount = request.Amount, PaymentType = "Collection", PaymentMethod = "Manual", ProviderTransactionId = reference,
                Purpose = order.OrderType == "Tour" ? "TourFee" : "RentalFee", Status = "Succeeded", CreatedAt = _timeProvider.GetUtcNow(), ProcessedAt = _timeProvider.GetUtcNow() }, cancellationToken);
            order.PaymentStatus = collected + request.Amount == order.TotalAmount ? "Paid" : "PartiallyPaid";
            if (order.PaymentStatus == "Paid")
            {
                order.Status = "Confirmed"; order.HoldExpiresAt = null; order.ReconciliationDeadlineAt = null;
                var promotions = await _unitOfWork.AppliedPromotions.QueryTracking()
                    .Where(x => x.OrderId == order.OrderId && x.Status == "Reserved").ToListAsync(cancellationToken);
                foreach (var promotion in promotions) { promotion.Status = "Applied"; promotion.AppliedAt = Now; }
            }
            order.UpdatedAt = _timeProvider.GetUtcNow();
            await AuditAsync("ManualPayment", "Order", order.OrderId, new { request.Amount, request.PaymentMethod, reference, reason }, cancellationToken);
        }, cancellationToken);
    }

    public async Task CancelOrderAsync(string orderCode, ReasonRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var reason = Required(request.Reason, "reason", 500);
        await AtomicAsync("order:" + orderCode, async () =>
        {
            var order = await ScopedOrders(true).Include(x => x.Payments).SingleOrDefaultAsync(x => x.OrderCode == orderCode, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng");
            if (order.Status == "Cancelled") return;
            Check(order.Status is "PendingPayment" or "Confirmed", "status", "Chỉ hủy đơn trước khi thực hiện dịch vụ");
            var assignments = await RentalAssignments(order.OrderId, true).ToListAsync(cancellationToken);
            Check(assignments.All(x => x.Status != "Issued"), "status", "Còn thiết bị đang được sử dụng");
            foreach (var assignment in assignments.Where(x => x.Status == "Reserved")) assignment.Status = "Cancelled";
            var participants = await _unitOfWork.BookingParticipants.QueryTracking().Where(x => x.BookingOrderId == order.OrderId && !x.IsDeleted).ToListAsync(cancellationToken);
            foreach (var participant in participants) { participant.Status = "Cancelled"; participant.UpdatedAt = _timeProvider.GetUtcNow(); }
            var invitations = await _unitOfWork.Repository<ParticipantInvitation>().QueryTracking()
                .Where(x => x.BookingOrderId == order.OrderId && x.RevokedAt == null && x.RedeemedAt == null).ToListAsync(cancellationToken);
            foreach (var invitation in invitations) invitation.RevokedAt = _timeProvider.GetUtcNow();
            var promotions = await _unitOfWork.AppliedPromotions.QueryTracking().Where(x => x.OrderId == order.OrderId && (x.Status == "Reserved" || x.Status == "Applied")).ToListAsync(cancellationToken);
            foreach (var promotion in promotions) promotion.Status = "Reversed";
            foreach (var payment in order.Payments.Where(x => x.PaymentType == "Collection" && x.Status == "Succeeded").ToList())
            {
                var pendingOrPaidRefund = order.Payments.Where(x => x.OriginalPaymentId == payment.PaymentId && x.PaymentType == "Refund" && x.Status != "Cancelled").Sum(x => x.Amount);
                if (payment.Amount > pendingOrPaidRefund)
                    await _unitOfWork.Payments.AddAsync(new Payment { PaymentId = Guid.NewGuid(), OrderId = order.OrderId,
                        OriginalPaymentId = payment.PaymentId, PaymentType = "Refund", Purpose = payment.Purpose,
                        PaymentMethod = payment.PaymentMethod, Amount = payment.Amount - pendingOrPaidRefund, Status = "PendingApproval", CreatedAt = _timeProvider.GetUtcNow() }, cancellationToken);
            }
            order.Status = "Cancelled"; order.CancelReason = reason; order.CancelledAt = _timeProvider.GetUtcNow();
            order.HoldExpiresAt = null; order.ReconciliationDeadlineAt = null; order.UpdatedAt = _timeProvider.GetUtcNow();
            await AuditAsync("Cancel", "Order", order.OrderId, new { reason }, cancellationToken);
        }, cancellationToken);
    }
}
