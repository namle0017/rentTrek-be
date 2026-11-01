using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.DTOs.Payment.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Payments;

public sealed class StaffReconciliationService(IUnitOfWork uow, ICurrentUserService user, TimeProvider clock, IPaymentDataProtector protector, SePayWebhookService webhooks)
{
    private Guid Staff()
    {
        if (!user.IsAuthenticated || user.Role != "Staff") throw new ForbiddenException();
        return user.RequiredUserId;
    }
    public async Task<PagedResponse<ReconciliationItemResponse>> ListAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        Staff();
        if (page < 1 || pageSize is < 1 or > 100) throw new BadRequestException("Phân trang không hợp lệ.");
        var query = from r in uow.Repository<PaymentWebhookReceipt>().Query()
                    join t in uow.Repository<BankTransaction>().Query() on r.PaymentWebhookReceiptId equals t.ReceiptId into transfers
                    from t in transfers.DefaultIfEmpty()
                    where r.HasConflictingPayload || r.Status == "Unmatched" || r.Status == "Rejected" || t != null && t.Status == "NeedsReconciliation"
                    select new ReconciliationItemResponse
                    {
                        ReceiptId = r.PaymentWebhookReceiptId, BankTransactionId = t == null ? null : t.BankTransactionId,
                        OrderId = t == null ? null : t.OrderId, Amount = t == null ? null : t.Amount,
                        Status = t == null ? r.Status : t.Status, Reason = t == null ? r.FailureReason : t.ReconciliationReason,
                        HasConflict = r.HasConflictingPayload, ReceivedAt = r.ReceivedAt
                    };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.ReceivedAt).ThenBy(x => x.ReceiptId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new() { Items = rows, PageNumber = page, PageSize = pageSize, TotalItems = total };
    }
    public async Task<SePayWebhookRequest> GetReceiptAsync(Guid receiptId, CancellationToken ct = default)
    {
        Staff();
        var receipt = await uow.Repository<PaymentWebhookReceipt>().GetByIdAsync(receiptId, ct) ?? throw new NotFoundException();
        return JsonSerializer.Deserialize<SePayWebhookRequest>(protector.Unprotect(receipt.ProtectedPayload), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ConflictException("Không đọc được receipt.");
    }

    public async Task MatchAsync(Guid bankTransactionId, Guid paymentId, string reason, CancellationToken ct = default)
    {
        var actor = Staff(); reason = reason?.Trim() ?? "";
        if (reason.Length is < 1 or > 2000) throw new BadRequestException("Cần lý do đối soát.");
        var info = await uow.Repository<BankTransaction>().Query().SingleOrDefaultAsync(x => x.BankTransactionId == bankTransactionId, ct) ?? throw new NotFoundException();
        var candidate = await uow.Payments.Query().SingleOrDefaultAsync(x => x.PaymentId == paymentId && x.PaymentType == "Collection", ct) ?? throw new NotFoundException();
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireIdentityLockAsync($"sepay:receipt:{info.ReceiptId:N}", ct);
            await uow.Payments.AcquireOrderLockAsync(candidate.OrderId, ct);
            var tx = await uow.Repository<BankTransaction>().QueryTracking().SingleAsync(x => x.BankTransactionId == bankTransactionId, ct);
            if (tx.PaymentId == paymentId && tx.AppliedAt is not null) { await uow.CommitAsync(ct); return; }
            var receipt = await uow.Repository<PaymentWebhookReceipt>().QueryTracking().SingleAsync(x => x.PaymentWebhookReceiptId == tx.ReceiptId, ct);
            if (tx.OrderId is not null || tx.PaymentId is not null || tx.Status != "Unmatched" || receipt.HasConflictingPayload || receipt.Status != "Unmatched")
                throw new ConflictException("Giao dịch không thể ghép tự động hoặc đã được xử lý.", "RECONCILIATION_STATE_INVALID");
            var order = await uow.Payments.GetOrderAsync(candidate.OrderId, cancellationToken: ct) ?? throw new NotFoundException();
            var payment = await uow.Payments.QueryTracking().SingleAsync(x => x.PaymentId == paymentId, ct);
            tx.OrderId = order.OrderId; tx.PaymentId = payment.PaymentId;
            await uow.SaveChangesAsync(ct);
            await webhooks.ApplyAsync(order, payment, tx, ct);
            receipt.Status = "Processed"; receipt.FailureReason = null;
            await PaymentEvents.AuditAsync(uow, clock, "Payment.ManuallyMatched", nameof(BankTransaction), bankTransactionId, actor, new { paymentId, reason }, ct);
            await uow.CommitAsync(ct);
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task AcceptAsync(Guid orderId, CancellationToken ct = default)
    {
        var actor = Staff();
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireOrderLockAsync(orderId, ct);
            var order = await uow.Payments.GetOrderAsync(orderId, cancellationToken: ct) ?? throw new NotFoundException();
            if (order.Status == "Confirmed" && order.PaymentStatus == "Paid") { await uow.CommitAsync(ct); return; }
            if (order.Status != "PendingPayment" || order.PaymentStatus is not ("Unpaid" or "NeedsReconciliation") ||
                order.HoldExpiresAt is null || clock.GetUtcNow() >= order.HoldExpiresAt)
                throw new ConflictException("Đơn không còn giữ chỗ để chấp nhận thanh toán.", "ORDER_NOT_RECONCILABLE");
            if (await uow.Repository<RefundRequest>().Query().AnyAsync(x => x.OrderId == orderId && (x.Status == "PendingReview" || x.Status == "Approved"), ct)
                || await uow.Payments.Query().AnyAsync(x => x.OrderId == orderId && x.PaymentType == "Refund"
                    && (x.Status == "Pending" || x.Status == "PendingApproval" || x.Status == "Succeeded"), ct))
                throw new ConflictException("Đơn đang có yêu cầu hoàn tiền.", "REFUND_IN_PROGRESS");
            var sources = await uow.Repository<BankTransaction>().QueryTracking().Where(x => x.OrderId == orderId && x.AppliedAt != null)
                .OrderBy(x => x.ReceivedAt).ThenBy(x => x.BankTransactionId).ToListAsync(ct);
            if (sources.Sum(x => x.Amount) < order.TotalAmount || sources.Any(x => x.PaymentId is null))
                throw new ConflictException("Chưa đủ tiền đã xác minh.", "RECONCILIATION_AMOUNT_INSUFFICIENT");
            var sourceIds = sources.Select(x => x.ReceiptId).ToArray();
            if (await uow.Repository<PaymentWebhookReceipt>().Query().AnyAsync(x => sourceIds.Contains(x.PaymentWebhookReceiptId) && x.HasConflictingPayload, ct))
                throw new ConflictException("Nguồn tiền có payload mâu thuẫn.", "RECONCILIATION_CONFLICT");
            var remaining = order.TotalAmount;
            foreach (var source in sources)
            {
                source.AllocatedAmount = Math.Min(remaining, source.Amount); remaining -= source.AllocatedAmount;
                source.Status = source.AllocatedAmount == source.Amount ? "Applied" : "NeedsReconciliation";
                source.ReconciliationReason = source.AllocatedAmount < source.Amount ? "EXCESS" : null;
            }
            foreach (var group in sources.GroupBy(x => x.PaymentId!.Value))
            {
                var payment = await uow.Payments.QueryTracking().SingleAsync(x => x.PaymentId == group.Key, ct);
                payment.ReceivedAmount = group.Sum(x => x.Amount); payment.Status = "Succeeded"; payment.ProcessedAt = clock.GetUtcNow();
            }
            order.Status = "Confirmed"; order.PaymentStatus = "Paid"; order.UpdatedAt = clock.GetUtcNow();
            await PaymentEvents.AuditAsync(uow, clock, "Payment.ReconciliationAccepted", nameof(Order), orderId, actor, new { order.TotalAmount }, ct);
            await PaymentEvents.NotifyAsync(uow, clock, order.CustomerUserId, "PaymentSucceeded", orderId, "Thanh toán đã được đối soát và xác nhận.", ct);
            await uow.CommitAsync(ct);
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }
}
