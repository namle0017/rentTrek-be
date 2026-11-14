using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(AppDbContext context) : base(context)
    {
    }

    public Task AcquireOrderLockAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(_context, $"payment:order:{orderId:N}", cancellationToken);

    public Task<Order?> GetOrderAsync(Guid orderId, Guid? customerUserId = null, CancellationToken cancellationToken = default)
        // Kèm RentalOrder để tính số phải trả của đơn thuê (tiền thuê + cọc)
        => _context.Orders
            .Include(o => o.RentalOrder)
            .SingleOrDefaultAsync(
                o => o.OrderId == orderId && (customerUserId == null || o.CustomerUserId == customerUserId),
                cancellationToken);

    public Task<Payment?> GetLatestPendingCollectionAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _dbSet
            .Where(p => p.OrderId == orderId && p.PaymentType == "Collection" && p.Status == "Pending")
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Payment?> GetOwnedAsync(
        Guid paymentId,
        Guid orderId,
        Guid customerUserId,
        CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Include(p => p.Order)
            .SingleOrDefaultAsync(
                p => p.PaymentId == paymentId && p.OrderId == orderId && p.Order.CustomerUserId == customerUserId,
                cancellationToken);

    // ponytail: quét toàn bảng payments theo biểu thức; thêm cột transfer_code có index nếu bảng lớn
    public Task<Payment?> FindByIdPrefixAsync(string paymentIdPrefix, CancellationToken cancellationToken = default)
        => _dbSet
            .FromSqlInterpolated($"SELECT * FROM payments WHERE left(replace(payment_id::text, '-', ''), 16) = {paymentIdPrefix}")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ProviderTransactionExistsAsync(string providerTransactionId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(p => p.ProviderTransactionId == providerTransactionId, cancellationToken);

    public Task<(IReadOnlyList<AdminPaymentRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminPaymentFilter filter, CancellationToken cancellationToken = default)
    {
        var refundCases = _context.RefundCases;
        var rules = _context.RefundPolicyRules;
        return AdminFiltered(filter)
            .SortBy(filter.SortBy, filter.IsDescending, nameof(Payment.CreatedAt), nameof(Payment.Amount), nameof(Payment.Status))
            .ToPageAsync(filter.PageNumber, filter.PageSize, p => new AdminPaymentRow
            {
                PaymentId = p.PaymentId,
                OrderId = p.OrderId,
                OrderCode = p.Order.OrderCode,
                OrderPaymentStatus = p.Order.PaymentStatus,
                PaymentType = p.PaymentType,
                Purpose = p.Purpose,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                ProviderTransactionId = p.ProviderTransactionId,
                Status = p.Status,
                FailureReason = refundCases.Where(r => r.RefundPaymentId == p.PaymentId).Select(r => r.RejectionReason).FirstOrDefault(),
                PolicyMinHoursBefore = rules.Where(r => r.RefundPolicyRuleId == p.RefundPolicyRuleId).Select(r => (int?)r.MinHoursBefore).FirstOrDefault(),
                PolicyRefundPercentage = rules.Where(r => r.RefundPolicyRuleId == p.RefundPolicyRuleId).Select(r => (decimal?)r.RefundPercentage).FirstOrDefault(),
                HasRefundCase = refundCases.Any(r => r.RefundPaymentId == p.PaymentId),
                CreatedAt = p.CreatedAt
            }, cancellationToken);
    }

    public async Task<AdminPaymentStatsRow> GetAdminStatsAsync(AdminPaymentFilter filter, CancellationToken cancellationToken = default)
    {
        // Gom theo (loại, trạng thái) ở DB rồi cộng ở bộ nhớ: tối đa vài chục nhóm
        var buckets = await AdminFiltered(filter)
            .GroupBy(p => new { p.PaymentType, p.Status })
            .Select(g => new { g.Key.PaymentType, g.Key.Status, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);

        var pending = buckets.Where(b => b.Status is "Pending" or "PendingApproval").ToList();
        return new AdminPaymentStatsRow
        {
            TotalCollectionSucceeded = buckets.Where(b => b.PaymentType == "Collection" && b.Status == "Succeeded").Sum(b => b.Amount),
            TotalRefundSucceeded = buckets.Where(b => b.PaymentType == "Refund" && b.Status == "Succeeded").Sum(b => b.Amount),
            PendingCount = pending.Sum(b => b.Count),
            PendingAmount = pending.Sum(b => b.Amount)
        };
    }

    private IQueryable<Payment> AdminFiltered(AdminPaymentFilter filter)
    {
        var query = _dbSet.AsNoTracking();
        if (filter.ScopeUserId.HasValue)
        {
            var orders = OrderRepository.Scoped(_context.Orders, filter.ScopeUserId);
            query = query.Where(p => orders.Any(o => o.OrderId == p.OrderId));
        }
        if (filter.PaymentType != null) query = query.Where(p => p.PaymentType == filter.PaymentType);
        if (filter.Purpose != null) query = query.Where(p => p.Purpose == filter.Purpose);
        if (filter.Status != null) query = query.Where(p => p.Status == filter.Status);
        if (filter.Tab is "PendingApproval" or "Pending" or "Failed")
            query = query.Where(p => p.PaymentType == "Refund" && p.Status == filter.Tab);
        if (filter.Tab == "NeedsReconciliation")
            query = query.Where(p => p.PaymentType == "Collection" && p.Status == "Pending" && p.ProviderTransactionId != null
                && p.Order.PaymentStatus == "NeedsReconciliation");
        if (filter.FromDate.HasValue)
        {
            var from = ToUtcOffset(filter.FromDate.Value);
            query = query.Where(p => p.CreatedAt >= from);
        }
        if (filter.ToDate.HasValue)
        {
            var to = ToUtcOffset(filter.ToDate.Value);
            query = query.Where(p => p.CreatedAt <= to);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(p => p.Order.OrderCode.ToLower().Contains(search)
                || (p.ProviderTransactionId != null && p.ProviderTransactionId.ToLower().Contains(search)));
        }
        return query;
    }

    public Task<Guid?> GetOrderIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().Where(p => p.PaymentId == paymentId).Select(p => (Guid?)p.OrderId).SingleOrDefaultAsync(cancellationToken);

    public Task<Payment?> GetForUpdateAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => _dbSet
            .Include(p => p.Order)
                .ThenInclude(o => o.Payments)
            .SingleOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken);

    public Task<bool> HasRefundCaseAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => _context.RefundCases.AnyAsync(r => r.RefundPaymentId == paymentId, cancellationToken);

    private static DateTimeOffset ToUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}
