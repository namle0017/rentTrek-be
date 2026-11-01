using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
{
    public Task AcquireIdentityLockAsync(string identity, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(_context, identity, cancellationToken);
    public PaymentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task AcquireOrderLockAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await _context.Orders.FromSqlInterpolated($"SELECT * FROM orders WHERE order_id = {orderId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<Order?> GetOrderAsync(Guid orderId, Guid? customerUserId = null, CancellationToken cancellationToken = default)
        => _context.Orders.SingleOrDefaultAsync(
            o => o.OrderId == orderId && (customerUserId == null || o.CustomerUserId == customerUserId),
            cancellationToken);

    public Task<Payment?> GetLatestPendingCollectionAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _dbSet
            .Where(p => p.OrderId == orderId && p.PaymentType == "Collection" && p.Status == "Pending" && p.ProviderTransactionId == null)
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
}
