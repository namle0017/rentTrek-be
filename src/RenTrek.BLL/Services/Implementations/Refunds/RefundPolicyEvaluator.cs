using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Refunds;

internal sealed class RefundPolicyEvaluator(IUnitOfWork uow, TimeProvider clock)
{
    public static bool Active(string status) => status is "PendingReview" or "Approved";

    public async Task<(RefundPolicySnapshotResult? Snapshot, decimal Available, string? Reason)> EvaluateAsync(Order order, string reasonCode, CancellationToken ct)
    {
        if (reasonCode is not ("Cancellation" or "ServiceIssue" or "PaymentIssue"))
            throw new BadRequestException("Lý do hoàn chưa được hỗ trợ.", "REFUND_REASON_UNSUPPORTED");
        if (reasonCode == "Cancellation" && order.Status != "Cancelled") return (null, 0, "ORDER_NOT_CANCELLED");
        var policy = await uow.RefundPolicies.Query().SingleOrDefaultAsync(x => x.RefundPolicyId == order.RefundPolicyId && !x.IsDeleted, ct);
        if (policy is null) return (null, 0, "REFUND_POLICY_UNAVAILABLE");
        DateTimeOffset? start;
        if (order.OrderType == "TripBooking")
            start = await uow.Repository<TripBookingOrder>().Query().Where(x => x.OrderId == order.OrderId).Select(x => (DateTimeOffset?)x.Trip.StartAt).SingleOrDefaultAsync(ct);
        else
        {
            var rentalStart = await uow.RentalOrders.Query().Where(x => x.OrderId == order.OrderId).Select(x => (DateTime?)x.RentalStartAt).SingleOrDefaultAsync(ct);
            start = rentalStart is null ? null : new DateTimeOffset(DateTime.SpecifyKind(rentalStart.Value, DateTimeKind.Utc));
        }
        if (start is null) return (null, 0, "SERVICE_START_UNAVAILABLE");
        var evaluatedAt = order.Status == "Cancelled" && order.CancelledAt is not null ? order.CancelledAt.Value : clock.GetUtcNow();
        var hours = Math.Floor((start.Value - evaluatedAt).TotalHours);
        var rule = await uow.RefundPolicyRules.Query().Where(x => x.RefundPolicyId == policy.RefundPolicyId && !x.IsDeleted && x.MinHoursBefore <= hours)
            .OrderByDescending(x => x.MinHoursBefore).FirstOrDefaultAsync(ct);
        if (hours < 0 || rule is null || rule.RefundPercentage == 0) return (null, 0, "POLICY_NOT_ELIGIBLE");
        if (rule.RefundPercentage is < 0 or > 100) throw new ConflictException("Refund policy không hợp lệ.", "REFUND_POLICY_INVALID");
        var collected = await CollectedAsync(order.OrderId, ct);
        if (collected <= 0) return (null, 0, "VERIFIED_COLLECTION_REQUIRED");
        var basis = order.TotalAmount;
        var percentage = rule.RefundPercentage;
        var previous = await uow.Repository<RefundRequest>().Query().Where(x => x.OrderId == order.OrderId).OrderBy(x => x.SubmittedAt).FirstOrDefaultAsync(ct);
        if (previous is not null)
        {
            var prior = Snapshot(previous);
            basis = Math.Min(basis, prior.PolicyBase);
            percentage = Math.Min(percentage, prior.Percentage);
        }
        var entitlement = Math.Floor(Math.Min(basis, collected) * percentage / 100m);
        var snapshot = new RefundPolicySnapshotResult(policy.RefundPolicyId, policy.Version, rule.RefundPolicyRuleId, evaluatedAt, start.Value, basis, percentage, entitlement);
        var (done, reserved) = await TotalsAsync(order.OrderId, ct);
        var available = Math.Max(0, Math.Min(entitlement - done - reserved, collected - done - reserved));
        return (snapshot, available, available > 0 ? null : "REFUND_ENTITLEMENT_EXHAUSTED");
    }
    public Task<decimal> CollectedAsync(Guid orderId, CancellationToken ct)
        => uow.Repository<BankTransaction>().Query().Where(x => x.OrderId == orderId && x.AppliedAt != null).SumAsync(x => x.Amount, ct);

    public async Task<(decimal Done, decimal Reserved)> TotalsAsync(Guid orderId, CancellationToken ct)
    {
        var refunds = uow.Payments.Query().Where(x => x.OrderId == orderId && x.PaymentType == "Refund");
        var done = await refunds.Where(x => x.Status == "Succeeded").SumAsync(x => x.Amount, ct);
        var legacyPending = await refunds.Where(x => x.Status == "Pending" || x.Status == "PendingApproval").SumAsync(x => x.Amount, ct);
        var reserved = await uow.Repository<RefundRequest>().Query().Where(x => x.OrderId == orderId && (x.Status == "PendingReview" || x.Status == "Approved")).SumAsync(x => x.Amount, ct);
        return (done, reserved + legacyPending);
    }
    public async Task ValidateReservedAsync(RefundRequest request, CancellationToken ct)
    {
        var (done, reserved) = await TotalsAsync(request.OrderId, ct);
        var snapshot = Snapshot(request);
        var collected = await CollectedAsync(request.OrderId, ct);
        var cap = Math.Floor(Math.Min(snapshot.PolicyBase, collected) * snapshot.Percentage / 100m);
        if (request.Amount > snapshot.Entitlement || done + reserved > cap || done + reserved > collected)
            throw new ConflictException("Hạn mức hoàn cần đối soát lại.", "REFUND_BUDGET_CHANGED");
    }
    public static RefundPolicySnapshotResult Snapshot(RefundRequest request) => JsonSerializer.Deserialize<RefundPolicySnapshotResult>(request.PolicySnapshot)
        ?? throw new ConflictException("Thiếu policy snapshot.", "REFUND_POLICY_UNAVAILABLE");
}
