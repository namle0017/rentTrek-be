using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho bậc giá thuê qua EF Core.
/// </summary>
public class RentalPriceTierRepository : GenericRepository<RentalPriceTier>, IRentalPriceTierRepository
{
    public RentalPriceTierRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<RentalPriceTierRow>> GetForVariantsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default)
    {
        if (variantIds.Count == 0)
            return [];

        var ids = variantIds.Distinct().ToArray();
        return await ActiveTiers()
            .Where(t => t.ProductVariantId != null && ids.Contains(t.ProductVariantId.Value))
            .OrderBy(t => t.MinDays)
            .Select(ToRow)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RentalPriceTierRow>> GetForBundlesAsync(
        IReadOnlyCollection<Guid> bundleIds,
        CancellationToken cancellationToken = default)
    {
        if (bundleIds.Count == 0)
            return [];

        var ids = bundleIds.Distinct().ToArray();
        return await ActiveTiers()
            .Where(t => t.BundleId != null && ids.Contains(t.BundleId.Value))
            .OrderBy(t => t.MinDays)
            .Select(ToRow)
            .ToListAsync(cancellationToken);
    }

    // Query filter mặc định của RentalPriceTier yêu cầu ProductVariant != null nên sẽ loại hết bậc giá của combo
    private IQueryable<RentalPriceTier> ActiveTiers()
        => _dbSet.IgnoreQueryFilters().AsNoTracking().Where(t => !t.IsDeleted);

    private static readonly Expression<Func<RentalPriceTier, RentalPriceTierRow>> ToRow =
        t => new RentalPriceTierRow
        {
            Id = t.Id,
            VariantId = t.ProductVariantId,
            BundleId = t.BundleId,
            MinDays = t.MinDays,
            MaxDays = t.MaxDays,
            DailyPrice = t.DailyPrice
        };
}
