using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Data;

namespace RenTrek.API.Health;

public sealed class PaymentReadinessHealthCheck(AppDbContext db, IPaymentDataProtector protector) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT transfer_code,received_amount FROM payments LIMIT 0; SELECT protected_payload FROM payment_webhook_receipts LIMIT 0; SELECT bank_transaction_id FROM bank_transactions LIMIT 0; SELECT protected_bank_details,policy_snapshot FROM refund_requests LIMIT 0; SELECT refund_allocation_id FROM refund_allocations LIMIT 0; SELECT key FROM payment_idempotency_keys LIMIT 0;", ct);
            if (protector.Unprotect(protector.Protect("readiness")) != "readiness") return HealthCheckResult.Unhealthy("Payment protection unavailable.");
            return HealthCheckResult.Healthy();
        }
        catch (Exception) { return HealthCheckResult.Unhealthy("Payment schema or data protection unavailable."); }
    }
}
