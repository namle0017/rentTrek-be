using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.API.BackgroundJobs;

public sealed class PaymentMaintenanceRunner(IServiceScopeFactory scopes, TimeProvider clock, ILogger<PaymentMaintenanceRunner> logger)
{
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        Guid[] receipts; Guid[] expired;
        using (var read = scopes.CreateScope())
        {
            var uow = read.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var now = clock.GetUtcNow();
            receipts = await uow.Repository<PaymentWebhookReceipt>().Query()
                .Where(x => (x.Status == "Received" || x.Status == "Retry") && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
                .OrderBy(x => x.ReceivedAt).ThenBy(x => x.PaymentWebhookReceiptId).Take(50).Select(x => x.PaymentWebhookReceiptId).ToArrayAsync(ct);
            expired = await uow.Orders.Query().Where(x => x.Status == "PendingPayment" && x.HoldExpiresAt != null && x.HoldExpiresAt <= now)
                .OrderBy(x => x.HoldExpiresAt).ThenBy(x => x.OrderId).Take(50).Select(x => x.OrderId).ToArrayAsync(ct);
        }
        foreach (var id in receipts)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<SePayWebhookService>().ProcessAsync(id, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Payment receipt {ReceiptId} processing failed ({ErrorType}).", id, ex.GetType().Name);
                await ScheduleRetryAsync(id, ct);
            }
        }
        foreach (var id in expired)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OrderPaymentLifecycleService>().ExpireAsync(id, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning("Order {OrderId} expiry failed ({ErrorType}).", id, ex.GetType().Name); }
        }
        // Refund requests are deliberately never executed or completed here.
    }
    private async Task ScheduleRetryAsync(Guid id, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.BeginTransactionAsync(ct);
        try
        {
            await uow.Payments.AcquireIdentityLockAsync($"sepay:receipt:{id:N}", ct);
            var r = await uow.Repository<PaymentWebhookReceipt>().QueryTracking().SingleOrDefaultAsync(x => x.PaymentWebhookReceiptId == id, ct);
            if (r is not null && r.Status is "Received" or "Retry")
            {
                r.Attempts++;
                r.Status = r.Attempts >= 12 ? "Unmatched" : "Retry";
                r.FailureReason = r.Attempts >= 12 ? "RETRY_EXHAUSTED_REVIEW_REQUIRED" : "PROCESSING_RETRY";
                r.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(r.Attempts, 8))) + Random.Shared.Next(0, 4));
            }
            await uow.CommitAsync(ct);
        }
        catch { await uow.RollbackAsync(CancellationToken.None); throw; }
    }
}
