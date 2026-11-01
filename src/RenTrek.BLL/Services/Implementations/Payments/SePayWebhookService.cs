using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Payments;

public sealed partial class SePayWebhookService(IUnitOfWork uow, TimeProvider clock, IOptions<SePaySettings> options, IPaymentDataProtector protector)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [GeneratedRegex("(?<![A-Z0-9])RTP[0-9A-F]{16}(?![A-Z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex CodeRegex();

    public async Task<Guid> ReceiveAsync(string rawBody, string? authorization, string? signature = null, string? timestamp = null, CancellationToken ct = default)
    {
        if (!Authenticate(rawBody, authorization, signature, timestamp))
            throw new UnauthorizedException("Webhook không được xác thực.");
        SePayWebhookRequest payload;
        try { payload = JsonSerializer.Deserialize<SePayWebhookRequest>(rawBody, Json) ?? throw new JsonException(); }
        catch (JsonException) { throw new BadRequestException("Webhook JSON không hợp lệ.", "WEBHOOK_INVALID"); }
        if (payload.Id <= 0) throw new BadRequestException("Webhook thiếu transaction ID hợp lệ.", "WEBHOOK_INVALID_ID");
        var eventId = payload.Id.ToString(CultureInfo.InvariantCulture);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireIdentityLockAsync($"sepay:event:{eventId}", ct);
            var existing = await uow.Repository<PaymentWebhookReceipt>().QueryTracking()
                .SingleOrDefaultAsync(x => x.Provider == "SePay" && x.ProviderEventId == eventId, ct);
            if (existing is not null)
            {
                if (existing.PayloadHash != hash)
                {
                    existing.HasConflictingPayload = true;
                    await PaymentEvents.AuditAsync(uow, clock, "Payment.WebhookConflict", nameof(PaymentWebhookReceipt), existing.PaymentWebhookReceiptId, null, new { eventId }, ct);
                }
                await uow.CommitAsync(ct);
                return existing.PaymentWebhookReceiptId;
            }
            var receipt = new PaymentWebhookReceipt
            {
                ProviderEventId = eventId, PayloadHash = hash, ProtectedPayload = protector.Protect(rawBody), ReceivedAt = clock.GetUtcNow()
            };
            await uow.Repository<PaymentWebhookReceipt>().AddAsync(receipt, ct);
            await uow.CommitAsync(ct);
            return receipt.PaymentWebhookReceiptId;
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task ProcessAsync(Guid receiptId, CancellationToken ct = default)
    {
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireIdentityLockAsync($"sepay:receipt:{receiptId:N}", ct);
            var receipt = await uow.Repository<PaymentWebhookReceipt>().QueryTracking().SingleOrDefaultAsync(x => x.PaymentWebhookReceiptId == receiptId, ct);
            if (receipt is null || receipt.Status is not ("Received" or "Retry")) { await uow.CommitAsync(ct); return; }
            receipt.Attempts++;
            if (receipt.HasConflictingPayload)
            {
                receipt.Status = "Unmatched"; receipt.FailureReason = "CONFLICTING_PAYLOAD";
                await uow.CommitAsync(ct); return;
            }
            var payload = JsonSerializer.Deserialize<SePayWebhookRequest>(protector.Unprotect(receipt.ProtectedPayload), Json)!;
            var settings = options.Value;
            var rejection = !string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase) ? "NOT_INBOUND"
                : !string.Equals(payload.AccountNumber, settings.AccountNumber, StringComparison.Ordinal) ? "ACCOUNT_MISMATCH"
                : !string.Equals(payload.Gateway, settings.BankName, StringComparison.OrdinalIgnoreCase) ? "BANK_MISMATCH"
                : !string.IsNullOrEmpty(settings.SubAccount) && payload.SubAccount != settings.SubAccount ? "SUBACCOUNT_MISMATCH"
                : payload.TransferAmount <= 0 || payload.TransferAmount != decimal.Truncate(payload.TransferAmount) || payload.TransferAmount > 999_999_999_999_999_999m ? "INVALID_AMOUNT"
                : null;
            if (rejection is not null)
            {
                receipt.Status = "Rejected"; receipt.FailureReason = rejection;
                await uow.CommitAsync(ct); return;
            }
            var codes = CodeRegex().Matches($"{payload.Code} {payload.Content}").Select(x => x.Value.ToUpperInvariant()).Distinct().ToArray();
            var candidate = codes.Length == 1
                ? await uow.Payments.Query().SingleOrDefaultAsync(x => x.PaymentType == "Collection" && x.TransferCode == codes[0], ct)
                : null;
            var tx = new BankTransaction
            {
                ReceiptId = receiptId, ProviderTransactionId = receipt.ProviderEventId, Amount = payload.TransferAmount,
                ReceivedAt = receipt.ReceivedAt, ReferenceCode = payload.ReferenceCode
            };
            await uow.Repository<BankTransaction>().AddAsync(tx, ct);
            if (candidate is null)
            {
                receipt.Status = "Unmatched"; receipt.FailureReason = codes.Length > 1 ? "AMBIGUOUS_CODE" : "PAYMENT_NOT_FOUND";
                tx.ReconciliationReason = receipt.FailureReason;
                await uow.CommitAsync(ct); return;
            }
            await uow.Payments.AcquireOrderLockAsync(candidate.OrderId, ct);
            var order = await uow.Payments.GetOrderAsync(candidate.OrderId, cancellationToken: ct)
                ?? throw new NotFoundException();
            var payment = await uow.Payments.QueryTracking().SingleAsync(x => x.PaymentId == candidate.PaymentId, ct);
            tx.OrderId = order.OrderId; tx.PaymentId = payment.PaymentId;
            await uow.SaveChangesAsync(ct);
            await ApplyAsync(order, payment, tx, ct);
            receipt.Status = "Processed"; receipt.FailureReason = null; receipt.NextAttemptAt = null;
            await uow.CommitAsync(ct);
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }

    internal async Task ApplyAsync(Order order, Payment payment, BankTransaction tx, CancellationToken ct)
    {
        if (tx.AppliedAt is not null) return;
        var now = clock.GetUtcNow();
        var previouslyAllocated = await uow.Repository<BankTransaction>().Query()
            .Where(x => x.OrderId == order.OrderId && x.BankTransactionId != tx.BankTransactionId)
            .SumAsync(x => x.AllocatedAmount, ct);
        var payable = order.Status == "PendingPayment" && order.PaymentStatus == "Unpaid"
            && order.HoldExpiresAt is not null && now < order.HoldExpiresAt;
        tx.AllocatedAmount = payable ? Math.Min(tx.Amount, Math.Max(0, order.TotalAmount - previouslyAllocated)) : 0;
        tx.AppliedAt = now;
        tx.Status = payable && tx.AllocatedAmount == tx.Amount ? "Applied" : "NeedsReconciliation";
        tx.ReconciliationReason = payable ? tx.AllocatedAmount < tx.Amount ? "EXCESS" : null
            : order.Status == "Confirmed" && order.PaymentStatus == "Paid" ? "EXTRA_PAYMENT" : "ORDER_NOT_PAYABLE";
        payment.ReceivedAmount = await uow.Repository<BankTransaction>().Query()
            .Where(x => x.PaymentId == payment.PaymentId).SumAsync(x => x.Amount, ct);
        if (payable && previouslyAllocated + tx.AllocatedAmount >= order.TotalAmount)
        {
            order.Status = "Confirmed"; order.PaymentStatus = "Paid";
            payment.Status = "Succeeded"; payment.ProcessedAt = now;
            await PaymentEvents.NotifyAsync(uow, clock, order.CustomerUserId, "PaymentSucceeded", order.OrderId, "Thanh toán thành công.", ct);
        }
        else if (!payable)
        {
            payment.Status = "Succeeded"; payment.ProcessedAt = now;
            if (order.PaymentStatus == "Unpaid") order.PaymentStatus = "NeedsReconciliation";
        }
        order.UpdatedAt = now;
        await PaymentEvents.AuditAsync(uow, clock, "Payment.TransactionApplied", nameof(BankTransaction), tx.BankTransactionId, null,
            new { order.OrderId, tx.Amount, tx.AllocatedAmount, tx.Status }, ct);
    }

    public bool Authenticate(string body, string? authorization, string? signature, string? timestamp)
    {
        var s = options.Value;
        if (s.AuthMode == "ApiKey")
            return !string.IsNullOrWhiteSpace(s.Api.Key) && Fixed($"Apikey {s.Api.Key}", authorization?.Trim());
        if (s.AuthMode != "Hmac" || string.IsNullOrWhiteSpace(s.WebhookSecret) ||
            !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return false;
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (seconds < now - 300 || seconds > now + 300) return false;
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(s.WebhookSecret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return Fixed("sha256=" + Convert.ToHexString(digest).ToLowerInvariant(), signature);
    }
    private static bool Fixed(string expected, string? actual) => actual is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
