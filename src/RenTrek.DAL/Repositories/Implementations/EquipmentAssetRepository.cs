using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho tài sản thiết bị (tồn khả dụng, chọn tài sản, khoá theo biến thể, quản lý kho O20/O21) qua EF Core.
/// </summary>
public class EquipmentAssetRepository : GenericRepository<EquipmentAsset>, IEquipmentAssetRepository
{
    public EquipmentAssetRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Dictionary<Guid, int>> GetAvailableCountsAsync(
        IReadOnlyCollection<Guid> variantIds,
        AssetAvailabilityFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (variantIds.Count == 0)
            return new Dictionary<Guid, int>();

        var ids = variantIds.Distinct().ToArray();
        var rows = await AvailableAssets(filter)
            .Where(a => ids.Contains(a.ProductVariantId))
            .GroupBy(a => a.ProductVariantId)
            .Select(g => new { VariantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.VariantId, x => x.Count);
    }

    public Task<List<Guid>> GetAvailableAssetIdsAsync(
        Guid variantId,
        AssetAvailabilityFilter filter,
        int take,
        CancellationToken cancellationToken = default)
        => AvailableAssets(filter)
            .Where(a => a.ProductVariantId == variantId)
            .OrderBy(a => a.AssetCode)
            .ThenBy(a => a.EquipmentAssetId)
            .Select(a => a.EquipmentAssetId)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task AcquireVariantLocksAsync(IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        // Sắp xếp cố định để mọi transaction khoá cùng thứ tự (tránh deadlock)
        foreach (var variantId in variantIds.Distinct().OrderBy(x => x))
            await PostgresAdvisoryLock.AcquireAsync(_context, $"rental:variant:{variantId:N}", cancellationToken);
    }

    public async Task<(IReadOnlyList<EquipmentAsset> Items, int TotalCount)> GetPagedAssetsAsync(
        string? search,
        Guid? productVariantId,
        string? assetStatus,
        int pageNumber,
        int pageSize,
        string sortBy,
        bool isDescending,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(a => a.ProductVariant)
                .ThenInclude(v => v.Product)
            .Include(a => a.AssetAssignments.Where(x => x.Status == "Issued" || x.Status == "Reserved"))
                .ThenInclude(x => x.RentalOrderItem!)
                .ThenInclude(roi => roi.RentalOrder)
                .ThenInclude(ro => ro.Order)
            .Include(a => a.AssetAssignments.Where(x => x.Status == "Issued" || x.Status == "Reserved"))
                .ThenInclude(x => x.TripProvidedItem!)
                .ThenInclude(tpi => tpi.Trip)
                .ThenInclude(t => t.Tour)
            .Where(a => !a.IsDeleted);

        if (productVariantId.HasValue)
        {
            query = query.Where(a => a.ProductVariantId == productVariantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(assetStatus))
        {
            var status = assetStatus.Trim();
            query = query.Where(a => a.AssetStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.AssetCode.ToLower().Contains(term) ||
                a.ProductVariant.Name.ToLower().Contains(term) ||
                a.ProductVariant.Product.Name.ToLower().Contains(term) ||
                a.ProductVariant.Sku.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy.ToLowerInvariant() switch
        {
            "assetcode" or "code" => isDescending ? query.OrderByDescending(a => a.AssetCode) : query.OrderBy(a => a.AssetCode),
            "assetstatus" or "status" => isDescending ? query.OrderByDescending(a => a.AssetStatus) : query.OrderBy(a => a.AssetStatus),
            "productname" => isDescending ? query.OrderByDescending(a => a.ProductVariant.Product.Name) : query.OrderBy(a => a.ProductVariant.Product.Name),
            "variantname" => isDescending ? query.OrderByDescending(a => a.ProductVariant.Name) : query.OrderBy(a => a.ProductVariant.Name),
            _ => isDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<EquipmentAsset?> GetByCodeWithDetailsAsync(string assetCode, CancellationToken cancellationToken = default)
    {
        var code = assetCode.Trim();
        return _dbSet
            .AsNoTracking()
            .Include(a => a.ProductVariant)
                .ThenInclude(v => v.Product)
            .Include(a => a.AssetAssignments)
                .ThenInclude(x => x.RentalOrderItem!)
                .ThenInclude(roi => roi.RentalOrder)
                .ThenInclude(ro => ro.Order)
            .Include(a => a.AssetAssignments)
                .ThenInclude(x => x.TripProvidedItem!)
                .ThenInclude(tpi => tpi.Trip)
                .ThenInclude(t => t.Tour)
            .Include(a => a.AssetAssignments)
                .ThenInclude(x => x.AppliedPromotion!)
                .ThenInclude(ap => ap.PromotionTier)
                .ThenInclude(pt => pt.Promotion)
            .Include(a => a.RentalCharges)
            .FirstOrDefaultAsync(a => a.AssetCode == code && !a.IsDeleted, cancellationToken);
    }

    public Task<EquipmentAsset?> GetByIdWithDetailsAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        return _dbSet
            .AsNoTracking()
            .Include(a => a.ProductVariant)
                .ThenInclude(v => v.Product)
            .Include(a => a.AssetAssignments)
                .ThenInclude(x => x.RentalOrderItem!)
                .ThenInclude(roi => roi.RentalOrder)
                .ThenInclude(ro => ro.Order)
            .Include(a => a.AssetAssignments)
                .ThenInclude(x => x.TripProvidedItem!)
                .ThenInclude(tpi => tpi.Trip)
                .ThenInclude(t => t.Tour)
            .Include(a => a.AssetAssignments)
                .ThenInclude(x => x.AppliedPromotion!)
                .ThenInclude(ap => ap.PromotionTier)
                .ThenInclude(pt => pt.Promotion)
            .Include(a => a.RentalCharges)
            .FirstOrDefaultAsync(a => a.EquipmentAssetId == assetId && !a.IsDeleted, cancellationToken);
    }

    public async Task<string> GenerateNextAssetCodeAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var cleanPrefix = prefix.Trim().ToUpperInvariant();
        var existingCodes = await _dbSet
            .AsNoTracking()
            .Where(a => a.AssetCode.StartsWith(cleanPrefix))
            .Select(a => a.AssetCode)
            .ToListAsync(cancellationToken);

        int maxSeq = 0;
        foreach (var code in existingCodes)
        {
            var match = Regex.Match(code.Substring(cleanPrefix.Length), @"\d+");
            if (match.Success && int.TryParse(match.Value, out var seq) && seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        return $"{cleanPrefix}{(maxSeq + 1):D4}";
    }

    public async Task<IReadOnlyList<EquipmentAsset>> GetAssetsForGanttAsync(
        DateTime fromDate,
        DateTime toDate,
        Guid? categoryId,
        Guid? productVariantId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(a => a.ProductVariant)
                .ThenInclude(v => v.Product)
                .ThenInclude(p => p.Category)
            .Include(a => a.AssetAssignments.Where(x => x.ReservedFrom < toDate && x.ReservedUntil > fromDate))
                .ThenInclude(x => x.RentalOrderItem!)
                .ThenInclude(roi => roi.RentalOrder)
                .ThenInclude(ro => ro.Order)
            .Include(a => a.AssetAssignments.Where(x => x.ReservedFrom < toDate && x.ReservedUntil > fromDate))
                .ThenInclude(x => x.TripProvidedItem!)
                .ThenInclude(tpi => tpi.Trip)
                .ThenInclude(t => t.Tour)
            .Include(a => a.AssetAssignments.Where(x => x.ReservedFrom < toDate && x.ReservedUntil > fromDate))
                .ThenInclude(x => x.AppliedPromotion!)
                .ThenInclude(ap => ap.PromotionTier)
                .ThenInclude(pt => pt.Promotion)
            .Where(a => !a.IsDeleted);

        if (productVariantId.HasValue)
        {
            query = query.Where(a => a.ProductVariantId == productVariantId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(a => a.ProductVariant.Product.CategoryId == categoryId.Value);
        }

        return await query
            .OrderBy(a => a.ProductVariant.Product.Name)
            .ThenBy(a => a.ProductVariant.Name)
            .ThenBy(a => a.AssetCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetBlockedAssetIdsAsync(
        IReadOnlyCollection<Guid> equipmentAssetIds,
        IReadOnlyCollection<Guid> excludedAssignmentIds,
        AssetAvailabilityFilter filter,
        CancellationToken cancellationToken = default)
    {
        var assetIds = equipmentAssetIds.ToArray();
        var excludedIds = excludedAssignmentIds.ToArray();
        var blockingStatuses = filter.BlockingAssignmentStatuses.ToArray();
        var issuedStatus = filter.IssuedStatus;
        var windowStart = filter.WindowStart;
        var windowEnd = filter.WindowEnd;
        var now = filter.NowUtc;

        return await _context.AssetAssignments
            .AsNoTracking()
            .Where(x => assetIds.Contains(x.EquipmentAssetId)
                        && !excludedIds.Contains(x.AssetAssignmentId)
                        && blockingStatuses.Contains(x.Status)
                        && x.ReservedFrom < windowEnd
                        && (x.ReservedUntil > windowStart
                            || (x.Status == issuedStatus && x.ReturnedAt == null && x.ReservedUntil <= now)))
            .Select(x => x.EquipmentAssetId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    // Tài sản khả dụng: không có phân bổ chặn giao khoảng thuê; phân bổ Issued chưa trả mà đã quá hạn vẫn chặn (BR-20)
    private IQueryable<EquipmentAsset> AvailableAssets(AssetAvailabilityFilter filter)
    {
        var rentableStatuses = filter.RentableAssetStatuses.ToArray();
        var blockingStatuses = filter.BlockingAssignmentStatuses.ToArray();
        var issuedStatus = filter.IssuedStatus;
        var windowStart = filter.WindowStart;
        var windowEnd = filter.WindowEnd;
        var now = filter.NowUtc;

        return _dbSet
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                        && rentableStatuses.Contains(a.AssetStatus)
                        && !a.AssetAssignments.Any(x =>
                            blockingStatuses.Contains(x.Status)
                            && x.ReservedFrom < windowEnd
                            && (x.ReservedUntil > windowStart
                                || (x.Status == issuedStatus && x.ReturnedAt == null && x.ReservedUntil <= now))));
    }
}
