using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Payments;

public sealed class OrderPaymentLifecycleService(IUnitOfWork uow, TimeProvider clock)
{
    public Task ExpireAsync(Guid orderId, CancellationToken ct = default) => ChangeAsync(orderId, null, true, ct);
    public Task CancelAsync(Guid userId, Guid orderId, CancellationToken ct = default) => ChangeAsync(orderId, userId, false, ct);

    private async Task ChangeAsync(Guid orderId, Guid? actor, bool expiry, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireOrderLockAsync(orderId, ct);
            var order = await uow.Payments.GetOrderAsync(orderId, actor, ct) ?? throw new NotFoundException();
            var now = clock.GetUtcNow();
            if (expiry)
            {
                if (order.Status != "PendingPayment" || order.HoldExpiresAt is null || order.HoldExpiresAt > now)
                { await uow.CommitAsync(ct); return; }
            }
            else
            {
                if (order.Status == "Cancelled") { await uow.CommitAsync(ct); return; }
                if (order.Status is not ("PendingPayment" or "Confirmed")) throw new ConflictException("Không thể hủy đơn ở trạng thái này.", "ORDER_NOT_CANCELLABLE");
                if (order.Status == "Confirmed")
                {
                    DateTimeOffset? start;
                    if (order.OrderType == "TripBooking")
                        start = await uow.Repository<TripBookingOrder>().Query().Where(x => x.OrderId == orderId).Select(x => (DateTimeOffset?)x.Trip.StartAt).SingleOrDefaultAsync(ct);
                    else
                    {
                        var rental = await uow.RentalOrders.Query().Where(x => x.OrderId == orderId).Select(x => (DateTime?)x.RentalStartAt).SingleOrDefaultAsync(ct);
                        start = rental is null ? null : new DateTimeOffset(DateTime.SpecifyKind(rental.Value, DateTimeKind.Utc));
                    }
                    if (start is null || start <= now) throw new ConflictException("Dịch vụ đã bắt đầu hoặc thiếu mốc thời gian.", "ORDER_NOT_CANCELLABLE");
                }
            }
            order.Status = expiry ? "Expired" : "Cancelled"; order.UpdatedAt = now;
            if (!expiry) order.CancelledAt = now;
            var money = await uow.Repository<BankTransaction>().Query().Where(x => x.OrderId == orderId).SumAsync(x => x.Amount, ct);
            if (money > 0 && order.PaymentStatus == "Unpaid") order.PaymentStatus = "NeedsReconciliation";
            var received = await uow.Repository<BankTransaction>().QueryTracking().Where(x => x.OrderId == orderId && x.Status == "Applied").ToListAsync(ct);
            foreach (var transfer in received)
            {
                transfer.Status = "NeedsReconciliation";
                transfer.ReconciliationReason = expiry ? "ORDER_EXPIRED" : "ORDER_CANCELLED";
            }
            var pending = await uow.Payments.QueryTracking().Where(x => x.OrderId == orderId && x.PaymentType == "Collection" && x.Status == "Pending").ToListAsync(ct);
            foreach (var p in pending) { p.Status = p.ReceivedAmount > 0 ? "Succeeded" : "Cancelled"; p.ProcessedAt = now; }
            var assignments = await uow.Repository<AssetAssignment>().QueryTracking().Where(x => x.Status == "Reserved" &&
                ((x.RentalOrderItem != null && x.RentalOrderItem.RentalOrderId == orderId) ||
                 (x.AppliedPromotion != null && x.AppliedPromotion.OrderId == orderId))).ToListAsync(ct);
            foreach (var assignment in assignments) assignment.Status = "Cancelled";
            var promotions = await uow.Repository<AppliedPromotion>().QueryTracking()
                .Where(x => x.OrderId == orderId && (x.Status == "Reserved" || x.Status == "Applied")).ToListAsync(ct);
            foreach (var promotion in promotions) promotion.Status = expiry ? "Expired" : "Reversed";
            await PaymentEvents.AuditAsync(uow, clock, expiry ? "Order.Expired" : "Order.Cancelled", nameof(Order), orderId, actor, new { order.Status, releasedAssignments = assignments.Count }, ct);
            await PaymentEvents.NotifyAsync(uow, clock, order.CustomerUserId, expiry ? "OrderExpired" : "OrderCancelled", orderId,
                expiry ? "Đơn đã hết hạn giữ chỗ." : "Đơn đã hủy. Tiền hoàn cần yêu cầu và được staff xử lý theo policy.", ct);
            await uow.CommitAsync(ct);
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }
}
