using RenTrek.DAL.Data;
using RenTrek.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace RenTrek.DAL.Repositories.Implementations;

public sealed class AdminOperationRepository(AppDbContext context) : IAdminOperationRepository
{
    public async Task AcquireLockAsync(string key, CancellationToken cancellationToken = default)
    {
        await PostgresAdvisoryLock.AcquireAsync(context, "admin:" + key, cancellationToken);
        if (key.StartsWith("order:", StringComparison.Ordinal))
        {
            var code = key[6..];
            var orderId = await context.Orders.AsNoTracking().Where(x => x.OrderCode == code)
                .Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(cancellationToken);
            if (orderId.HasValue)
                await PostgresAdvisoryLock.AcquireAsync(context, $"trip-booking:order:{orderId:N}", cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT order_id FROM orders WHERE order_code = {code} FOR UPDATE", cancellationToken);
        }
    }
}
