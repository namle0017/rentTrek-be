using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho tài sản thiết bị (tồn khả dụng, chọn tài sản, khoá theo biến thể) qua EF Core.
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

    // Tài sản khả dụng: không có phân bổ chặn giao khoảng thuê; phân bổ Issued chưa trả mà đã quá hạn vẫn chặn (BR-20)
    private IQueryable<EquipmentAsset> AvailableAssets(AssetAvailabilityFilter filter)
    {
        var rentableStatuses = filter.RentableAssetStatuses.ToArray();
        var blockingStatuses = filter.BlockingAssignmentStatuses.ToArray();
        var issuedStatus = filter.IssuedStatus;
        var windowStart = filter.WindowStart;
        var windowEnd = filter.WindowEnd;
        var now = filter.NowUtc;
        var nowOffset = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));

        return _dbSet
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                        && rentableStatuses.Contains(a.AssetStatus)
                        && !a.AssetAssignments.Any(x =>
                            blockingStatuses.Contains(x.Status)
                            // An unpaid rental hold no longer blocks inventory after its deadline.
                            && !(x.Status == "Reserved" && x.RentalOrderItem != null
                                && x.RentalOrderItem.RentalOrder.Order.Status == "PendingPayment"
                                && x.RentalOrderItem.RentalOrder.Order.PaymentStatus == "Unpaid"
                                && x.RentalOrderItem.RentalOrder.Order.HoldExpiresAt <= nowOffset)
                            && !(x.Status == "Reserved" && x.AppliedPromotion != null
                                && x.AppliedPromotion.Order.Status == "PendingPayment"
                                && x.AppliedPromotion.Order.PaymentStatus == "Unpaid"
                                && x.AppliedPromotion.Order.HoldExpiresAt <= nowOffset)
                            && x.ReservedFrom < windowEnd
                            && (x.ReservedUntil > windowStart
                                || (x.Status == issuedStatus && x.ReturnedAt == null && x.ReservedUntil <= now))));
    }
}
