using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho khuyến mãi (tra mã, đếm lượt dùng, khoá) qua EF Core.
/// </summary>
public class PromotionRepository : GenericRepository<Promotion>, IPromotionRepository
{
    public PromotionRepository(AppDbContext context) : base(context)
    {
    }

    public Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var lowerCode = code.Trim().ToLower();
        return _dbSet
            .AsNoTracking()
            .Include(p => p.PromotionTiers.Where(t => !t.IsDeleted))
            .Where(p => !p.IsDeleted && p.PromotionCode != null && p.PromotionCode.ToLower() == lowerCode)
            .OrderBy(p => p.PromotionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountUsageAsync(
        Guid promotionId,
        IReadOnlyCollection<string> countedStatuses,
        Guid? customerUserId,
        CancellationToken cancellationToken = default)
    {
        var statuses = countedStatuses.ToArray();
        // Query filter của Order đã loại đơn đã xoá
        var query = _context.Set<AppliedPromotion>()
            .AsNoTracking()
            .Where(ap => ap.PromotionTier.PromotionId == promotionId
                         && statuses.Contains(ap.Status)
                         && !ap.Order.IsDeleted);

        if (customerUserId.HasValue)
        {
            var userId = customerUserId.Value;
            query = query.Where(ap => ap.Order.CustomerUserId == userId);
        }

        return query.Select(ap => ap.OrderId).Distinct().CountAsync(cancellationToken);
    }

    public Task AcquireLockAsync(Guid promotionId, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(_context, $"rental:promotion:{promotionId:N}", cancellationToken);
}
