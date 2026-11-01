using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Refunds;

public sealed class RefundRequestService(IUnitOfWork uow, ICurrentUserService user, TimeProvider clock, IPaymentDataProtector protector)
{
    public async Task<RefundEligibilityResponse> PreviewAsync(Guid orderId, string reasonCode, CancellationToken ct = default)
    {
        var order = await OwnedOrderAsync(orderId, ct);
        var (snapshot, available, reason) = await new RefundPolicyEvaluator(uow, clock).EvaluateAsync(order, reasonCode, ct);
        return new(available > 0, reason, available, snapshot?.Version ?? 0, snapshot?.Percentage ?? 0);
    }
    public async Task<RefundRequestResponse> SubmitAsync(Guid orderId, CreateRefundRequest request, string idempotencyKey, CancellationToken ct = default)
    {
        var key = ValidateKey(idempotencyKey);
        var bank = ValidateBank(request);
        var reason = request.ReasonCode.Trim();
        var note = request.Note?.Trim();
        if (note?.Length > 2000) throw new BadRequestException("Nội dung quá dài.", "REFUND_INVALID");
        var hash = Hash(JsonSerializer.Serialize(new { orderId, bank, reason, note }));
        var actor = user.RequiredUserId;
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireIdentityLockAsync($"refund:submit:{actor:N}:{key}", ct);
            await uow.Payments.AcquireOrderLockAsync(orderId, ct);
            var order = await OwnedOrderAsync(orderId, ct);
            var replay = await uow.Repository<RefundRequest>().Query().SingleOrDefaultAsync(x => x.CustomerUserId == actor && x.IdempotencyKey == key, ct);
            if (replay is not null)
            {
                if (replay.RequestHash != hash) throw new ConflictException("Idempotency-Key đã dùng cho yêu cầu khác.", "IDEMPOTENCY_KEY_REUSED");
                await uow.CommitAsync(ct);
                var response = RefundRequestMapping.Map(replay, protector);
                response.Replayed = true;
                return response;
            }
            var (snapshot, available, failure) = await new RefundPolicyEvaluator(uow, clock).EvaluateAsync(order, reason, ct);
            if (snapshot is null || available <= 0) throw new ConflictException("Không đủ điều kiện hoàn theo policy.", failure);
            var r = new RefundRequest
            {
                OrderId = orderId, CustomerUserId = actor, IdempotencyKey = key, RequestHash = hash,
                ProtectedBankDetails = protector.Protect(JsonSerializer.Serialize(bank)),
                MaskedAccount = bank.AccountNumber.Length > 4 ? "****" + bank.AccountNumber[^4..] : "****",
                ReasonCode = reason, Note = note, Amount = available, RefundPolicyId = snapshot.PolicyId,
                RefundPolicyRuleId = snapshot.RuleId, PolicySnapshot = JsonSerializer.Serialize(snapshot), SubmittedAt = clock.GetUtcNow()
            };
            await uow.Repository<RefundRequest>().AddAsync(r, ct);
            await AllocateAsync(r, ct);
            await PaymentEvents.AuditAsync(uow, clock, "Refund.Requested", nameof(RefundRequest), r.RefundRequestId, actor, new { orderId, r.Amount, snapshot.Version }, ct);
            await uow.CommitAsync(ct);
            return RefundRequestMapping.Map(r, protector);
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }

    private async Task AllocateAsync(RefundRequest r, CancellationToken ct)
    {
        var sources = await uow.Repository<BankTransaction>().Query().Where(x => x.OrderId == r.OrderId && x.AppliedAt != null)
            .OrderBy(x => x.ReceivedAt).ThenBy(x => x.BankTransactionId).ToListAsync(ct);
        var remaining = r.Amount;
        foreach (var source in sources)
        {
            var used = await (from allocation in uow.Repository<RefundAllocation>().Query()
                              join request in uow.Repository<RefundRequest>().Query() on allocation.RefundRequestId equals request.RefundRequestId
                              where allocation.BankTransactionId == source.BankTransactionId && request.Status != "Rejected"
                              select allocation.Amount).SumAsync(ct);
            var amount = Math.Min(remaining, Math.Max(0, source.Amount - used));
            if (amount <= 0) continue;
            await uow.Repository<RefundAllocation>().AddAsync(new RefundAllocation { RefundRequestId = r.RefundRequestId, BankTransactionId = source.BankTransactionId, Amount = amount }, ct);
            remaining -= amount;
            if (remaining == 0) break;
        }
        if (remaining != 0) throw new ConflictException("Nguồn tiền hoàn cần đối soát.", "REFUND_SOURCE_UNAVAILABLE");
    }

    public async Task<RefundRequestResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var actor = user.RequiredUserId;
        var r = await uow.Repository<RefundRequest>().Query().SingleOrDefaultAsync(x => x.RefundRequestId == id && x.CustomerUserId == actor, ct)
            ?? throw new NotFoundException();
        return RefundRequestMapping.Map(r, protector);
    }
    public async Task<PagedResponse<RefundRequestResponse>> ListAsync(Guid orderId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        await OwnedOrderAsync(orderId, ct);
        if (page < 1 || pageSize is < 1 or > 100) throw new BadRequestException("Phân trang không hợp lệ.");
        var q = uow.Repository<RefundRequest>().Query().Where(x => x.OrderId == orderId && x.CustomerUserId == user.RequiredUserId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.RefundRequestId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new() { Items = rows.Select(x => RefundRequestMapping.Map(x, protector, detail: false)).ToArray(), PageNumber = page, PageSize = pageSize, TotalItems = total };
    }
    private async Task<Order> OwnedOrderAsync(Guid id, CancellationToken ct)
        => await uow.Payments.GetOrderAsync(id, user.RequiredUserId, ct) ?? throw new NotFoundException();
    internal static string ValidateKey(string? value)
    {
        var key = value?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new BadRequestException("Idempotency-Key phải có 1–100 ký tự.", "IDEMPOTENCY_KEY_REQUIRED");
        return key;
    }
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static RefundBankDetailsResponse ValidateBank(CreateRefundRequest r)
    {
        var bank = (r.BankCode ?? "").Trim().ToUpperInvariant(); var account = (r.AccountNumber ?? "").Trim(); var name = (r.AccountHolderName ?? "").Trim();
        if (!Regex.IsMatch(bank, "^[A-Z0-9_-]{2,30}$") || !Regex.IsMatch(account, "^[A-Za-z0-9]{4,40}$") ||
            name.Length is < 2 or > 100 || name.Any(char.IsControl) || string.IsNullOrWhiteSpace(r.ReasonCode))
            throw new BadRequestException("Thông tin tài khoản hoặc lý do không hợp lệ.", "REFUND_INVALID_BANK_DETAILS");
        return new(bank, account, name);
    }
}
