using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Refunds;

public sealed class StaffRefundRequestService(IUnitOfWork uow, ICurrentUserService user, TimeProvider clock, IPaymentDataProtector protector)
{
    private Guid Staff()
    {
        if (!user.IsAuthenticated || user.Role != "Staff") throw new ForbiddenException();
        return user.RequiredUserId;
    }
    public Task<RefundRequestResponse> ApproveAsync(Guid requestId, CancellationToken ct = default)
        => MutateAsync(requestId, async r =>
        {
            var actor = Staff();
            if (r.Status == "Approved" && r.AssignedStaffId == actor) return;
            if (r.Status != "PendingReview") throw InvalidState();
            await new RefundPolicyEvaluator(uow, clock).ValidateReservedAsync(r, ct);
            // Ensure the recipient snapshot is readable before approval.
            _ = protector.Unprotect(r.ProtectedBankDetails);
            r.Status = "Approved"; r.AssignedStaffId = actor; r.ReviewedBy = actor; r.ReviewedAt = clock.GetUtcNow();
            await RecordAsync(r, "Refund.Approved", "Yêu cầu hoàn tiền đã được duyệt, đang chờ chuyển khoản.", ct);
        }, ct);

    public Task<RefundRequestResponse> RejectAsync(Guid requestId, string reason, CancellationToken ct = default)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 1 or > 2000) throw new BadRequestException("Cần lý do từ chối 1–2000 ký tự.", "REFUND_REJECT_REASON_REQUIRED");
        return MutateAsync(requestId, async r =>
        {
            if (r.Status == "Rejected" && r.RejectReason == reason) return;
            if (r.Status != "PendingReview") throw InvalidState();
            r.Status = "Rejected"; r.RejectReason = reason; r.ReviewedBy = Staff(); r.ReviewedAt = clock.GetUtcNow();
            await RecordAsync(r, "Refund.Rejected", "Yêu cầu hoàn tiền đã bị từ chối. Xem lý do trong chi tiết.", ct);
        }, ct);
    }
    public Task<RefundRequestResponse> CompleteAsync(Guid requestId, CompleteRefundRequest request, string idempotencyKey, CancellationToken ct = default)
    {
        var key = RefundRequestService.ValidateKey(idempotencyKey);
        var reference = request.TransferReference?.Trim().ToUpperInvariant() ?? "";
        var evidence = request.EvidenceReference?.Trim();
        var transferredAt = request.TransferredAt.ToUniversalTime();
        if (reference.Length is < 1 or > 200 || reference.Any(char.IsControl) || evidence?.Length > 1000 ||
            transferredAt == default || transferredAt > clock.GetUtcNow())
            throw new BadRequestException("Thông tin xác nhận chuyển tiền không hợp lệ.", "REFUND_COMPLETION_INVALID");
        var hash = RefundRequestService.Hash(JsonSerializer.Serialize(new { reference, transferredAt, evidence }));
        return MutateAsync(requestId, async r =>
        {
            if (r.Status is not ("Approved" or "Done")) throw InvalidState();
            if (r.AssignedStaffId != Staff()) throw new ForbiddenException("Chỉ staff được phân công mới được xác nhận chuyển tiền.");
            if (r.Status == "Done")
            {
                if (r.CompletionHash != hash) throw new ConflictException("Yêu cầu đã hoàn tất với thông tin khác.", "REFUND_ALREADY_COMPLETED");
                return;
            }
            if (r.Status != "Approved") throw InvalidState();
            if (r.ReviewedAt is null || transferredAt < r.ReviewedAt)
                throw new BadRequestException("Thời gian chuyển tiền phải sau khi duyệt.", "REFUND_COMPLETION_INVALID");
            await new RefundPolicyEvaluator(uow, clock).ValidateReservedAsync(r, ct);
            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(), OrderId = r.OrderId, PaymentType = "Refund", Amount = r.Amount,
                PaymentMethod = "BankTransfer", Status = "Succeeded", CreatedAt = clock.GetUtcNow(), ProcessedAt = transferredAt,
                RefundPolicyRuleId = r.RefundPolicyRuleId, Purpose = "ManualRefund", ProviderTransactionId = $"MANUAL:{r.RefundRequestId:N}"
            };
            await uow.Payments.AddAsync(payment, ct);
            r.Status = "Done"; r.RefundPaymentId = payment.PaymentId; r.CompletedBy = Staff(); r.CompletedAt = clock.GetUtcNow();
            r.TransferReference = reference; r.TransferredAt = transferredAt; r.EvidenceReference = evidence;
            r.CompletionKey = key; r.CompletionHash = hash;
            await uow.SaveChangesAsync(ct);
            var order = await uow.Payments.GetOrderAsync(r.OrderId, cancellationToken: ct) ?? throw new NotFoundException();
            var (done, _) = await new RefundPolicyEvaluator(uow, clock).TotalsAsync(r.OrderId, ct);
            var collected = await new RefundPolicyEvaluator(uow, clock).CollectedAsync(r.OrderId, ct);
            order.PaymentStatus = done >= collected ? "Refunded" : "PartiallyRefunded"; order.UpdatedAt = clock.GetUtcNow();
            await RecordAsync(r, "Refund.Completed", "Staff đã xác nhận chuyển tiền hoàn thành công.", ct);
        }, ct);
    }

    private async Task<RefundRequestResponse> MutateAsync(Guid requestId, Func<RefundRequest, Task> action, CancellationToken ct)
    {
        Staff();
        var info = await uow.Repository<RefundRequest>().Query().SingleOrDefaultAsync(x => x.RefundRequestId == requestId, ct)
            ?? throw new NotFoundException();
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireOrderLockAsync(info.OrderId, ct);
            var r = await uow.Repository<RefundRequest>().QueryTracking().SingleAsync(x => x.RefundRequestId == requestId, ct);
            await action(r);
            await uow.CommitAsync(ct);
            return RefundRequestMapping.Map(r, protector, staff: true);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_refund_requests_transfer_reference" })
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw new ConflictException("Mã chuyển khoản đã được ghi nhận cho yêu cầu khác.", "REFUND_TRANSFER_ALREADY_RECORDED");
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }
    private async Task RecordAsync(RefundRequest r, string action, string message, CancellationToken ct)
    {
        await PaymentEvents.AuditAsync(uow, clock, action, nameof(RefundRequest), r.RefundRequestId, Staff(), new { r.OrderId, r.Amount, r.Status, r.TransferReference }, ct);
        await PaymentEvents.NotifyAsync(uow, clock, r.CustomerUserId, action, r.OrderId, message, ct);
    }
    public async Task<RefundRequestResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        Staff();
        var r = await uow.Repository<RefundRequest>().Query().SingleOrDefaultAsync(x => x.RefundRequestId == id, ct) ?? throw new NotFoundException();
        return RefundRequestMapping.Map(r, protector, staff: true);
    }
    public async Task<PagedResponse<RefundRequestResponse>> ListAsync(string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        Staff();
        if (page < 1 || pageSize is < 1 or > 100 || status is not (null or "PendingReview" or "Approved" or "Rejected" or "Done"))
            throw new BadRequestException("Bộ lọc/phân trang không hợp lệ.");
        var q = uow.Repository<RefundRequest>().Query();
        if (status is not null) q = q.Where(x => x.Status == status);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.SubmittedAt).ThenBy(x => x.RefundRequestId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new() { Items = rows.Select(x => RefundRequestMapping.Map(x, protector, detail: false, staff: true)).ToArray(), PageNumber = page, PageSize = pageSize, TotalItems = total };
    }
    private static ConflictException InvalidState() => new("Trạng thái yêu cầu không cho phép thao tác này.", "REFUND_STATE_INVALID");
}
