using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
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

    public Task<List<Promotion>> GetRunningWithCodeAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Include(p => p.PromotionTiers.Where(t => !t.IsDeleted))
            .Where(p => !p.IsDeleted && p.PromotionCode != null && p.StartAt <= nowUtc && p.EndAt >= nowUtc)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.EndAt)
            .ToListAsync(cancellationToken);

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

    public Task<(IReadOnlyList<AdminPromotionRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminPromotionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => !x.IsDeleted);
        if (filter.Status != null) query = query.Where(x => x.Status == filter.Status);
        if (filter.AppliesTo != null) query = query.Where(x => x.AppliesTo == filter.AppliesTo);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(search) || (x.PromotionCode != null && x.PromotionCode.ToLower().Contains(search)));
        }

        return query
            .SortBy(filter.SortBy, filter.IsDescending, nameof(Promotion.CreatedAt), nameof(Promotion.Name),
                nameof(Promotion.StartAt), nameof(Promotion.EndAt), nameof(Promotion.Priority))
            .ToPageAsync(filter.PageNumber, filter.PageSize, ToAdminRow, cancellationToken);
    }

    public Task<AdminPromotionRow?> GetAdminRowAsync(Guid promotionId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().Where(x => x.PromotionId == promotionId && !x.IsDeleted)
            .Select(ToAdminRow).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid exceptPromotionId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => x.PromotionId != exceptPromotionId && x.PromotionCode == code, cancellationToken);

    public Task<Promotion?> GetForUpdateAsync(Guid promotionId, CancellationToken cancellationToken = default)
        => _dbSet.SingleOrDefaultAsync(x => x.PromotionId == promotionId && !x.IsDeleted, cancellationToken);

    public Task<List<PromotionTier>> GetTiersAsync(Guid promotionId, CancellationToken cancellationToken = default)
        => _context.Set<PromotionTier>().Where(x => x.PromotionId == promotionId && !x.IsDeleted)
            .OrderBy(x => x.MinThreshold).ToListAsync(cancellationToken);

    public Task<int> CountActiveVariantsAsync(IReadOnlyCollection<Guid> productVariantIds, CancellationToken cancellationToken = default)
    {
        var ids = productVariantIds.ToArray();
        return _context.Set<ProductVariant>().CountAsync(x => ids.Contains(x.Id) && !x.IsDeleted, cancellationToken);
    }

    public Task<(IReadOnlyList<PromotionUsageRow> Items, int TotalCount)> GetUsagePagedAsync(
        PromotionUsageFilter filter, CancellationToken cancellationToken = default)
        => _context.Set<AppliedPromotion>().AsNoTracking().Where(x => x.PromotionTier.PromotionId == filter.PromotionId)
            .SortBy(filter.SortBy, filter.IsDescending, nameof(AppliedPromotion.AppliedAt))
            .ToPageAsync(filter.PageNumber, filter.PageSize, x => new PromotionUsageRow
            {
                AppliedPromotionId = x.AppliedPromotionId,
                OrderId = x.OrderId,
                OrderCode = x.Order.OrderCode,
                DiscountAmount = x.DiscountAmount,
                GiftQuantity = x.GiftQuantity,
                Status = x.Status,
                AppliedAt = x.AppliedAt
            }, cancellationToken);

    private static readonly Expression<Func<Promotion, AdminPromotionRow>> ToAdminRow = x => new AdminPromotionRow
    {
        PromotionId = x.PromotionId,
        Name = x.Name,
        PromotionCode = x.PromotionCode,
        Description = x.Description,
        StartAt = x.StartAt,
        EndAt = x.EndAt,
        AppliesTo = x.AppliesTo,
        ApplicationScope = x.ApplicationScope,
        UsedCount = x.PromotionTiers.SelectMany(t => t.AppliedPromotions)
            .Where(a => a.Status == "Applied" || a.Status == "Reserved").Select(a => a.OrderId).Distinct().Count(),
        MaxUses = x.MaxUses,
        MaxUsesPerUser = x.MaxUsesPerUser,
        IsStackable = x.IsStackable,
        Priority = x.Priority,
        Status = x.Status
    };
}
