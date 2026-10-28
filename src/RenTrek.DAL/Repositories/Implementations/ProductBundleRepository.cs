using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho combo thiết bị (danh sách, chi tiết, thông tin dựng dòng thuê) qua EF Core.
/// </summary>
/// <remarks>
/// Mọi truy vấn bỏ qua query filter mặc định (filter của RentalPriceTier loại hết bậc giá của combo vì yêu cầu
/// ProductVariant != null) và tự áp điều kiện chưa xoá một cách tường minh.
/// </remarks>
public class ProductBundleRepository : GenericRepository<ProductBundle>, IProductBundleRepository
{
    // Giá trị product_bundles.status / categories.status cho "đang bán" (xem RenTrek.BLL.Constants.ProductStatuses / CategoryStatuses)
    private const string ActiveStatus = "Active";

    public ProductBundleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<RentalBundleInfo>> GetActiveForRentalAsync(
        IReadOnlyCollection<Guid> bundleIds,
        CancellationToken cancellationToken = default)
    {
        if (bundleIds.Count == 0)
            return [];

        var ids = bundleIds.Distinct().ToArray();
        return await _dbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => ids.Contains(b.BundleId))
            .Select(b => new RentalBundleInfo
            {
                BundleId = b.BundleId,
                Name = b.Name,
                IsActive = !b.IsDeleted && b.Status == ActiveStatus,
                Components = b.ProductBundleItems
                    .Where(bi => !bi.IsDeleted)
                    .OrderBy(bi => bi.CreatedAt).ThenBy(bi => bi.BundleItemId)
                    .Select(bi => new RentalBundleComponentInfo
                    {
                        VariantId = bi.ProductVariantId,
                        VariantName = bi.ProductVariant.Name,
                        ProductName = bi.ProductVariant.Product.Name,
                        QuantityPerBundle = bi.Quantity,
                        IsActive = !bi.ProductVariant.IsDeleted && bi.ProductVariant.Status == CatalogStatus.Active
                                   && !bi.ProductVariant.Product.IsDeleted
                                   && bi.ProductVariant.Product.Status == CatalogStatus.Active
                    })
                    .ToList()
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ProductBundleListRow> Items, int TotalCount)> GetPagedActiveAsync(
        ProductBundleFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = Sort(VisibleBundles(filter), filter);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Max(1, filter.PageSize);
        var rows = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new ProductBundleListRow
            {
                Id = b.BundleId,
                Name = b.Name,
                Description = b.Description,
                CategoryId = b.CategoryId,
                CategoryName = b.Category.Name,
                CreatedAt = b.CreatedAt,
                PriceFrom = b.RentalPriceTiers.Where(t => !t.IsDeleted).Min(t => (decimal?)t.DailyPrice) ?? 0m
            })
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            var pageIds = rows.Select(r => r.Id).ToArray();
            var components = await LiveComponents()
                .Where(bi => pageIds.Contains(bi.BundleId))
                .OrderBy(bi => bi.CreatedAt).ThenBy(bi => bi.BundleItemId)
                .Select(ToComponentRow)
                .ToListAsync(cancellationToken);

            var byBundle = components.ToLookup(c => c.BundleId);
            foreach (var row in rows)
            {
                row.Components = byBundle[row.Id].ToList();
                row.CoverImageUrl = row.Components.Select(c => c.PrimaryImageUrl).FirstOrDefault(u => u != null);
            }
        }

        return (rows, totalCount);
    }

    public Task<List<ProductBundleComponentRow>> GetActiveComponentsAsync(
        ProductBundleFilter filter,
        CancellationToken cancellationToken = default)
    {
        var bundleIds = VisibleBundles(filter).Select(b => b.BundleId);
        return LiveComponents()
            .Where(bi => bundleIds.Contains(bi.BundleId))
            .OrderBy(bi => bi.CreatedAt).ThenBy(bi => bi.BundleItemId)
            .Select(ToComponentRow)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductBundleDetailRow?> GetActiveDetailAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        var bundle = await _dbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.BundleId == bundleId && !b.IsDeleted && b.Status == ActiveStatus)
            .Select(b => new ProductBundleDetailRow
            {
                Id = b.BundleId,
                Name = b.Name,
                Description = b.Description,
                Status = b.Status,
                CategoryId = b.CategoryId,
                CategoryName = b.Category.Name
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (bundle is null)
            return null;

        bundle.PriceTiers = await _context.Set<RentalPriceTier>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.BundleId == bundleId && !t.IsDeleted)
            .OrderBy(t => t.MinDays)
            .Select(t => new RentalPriceTierRow
            {
                Id = t.Id,
                VariantId = t.ProductVariantId,
                BundleId = t.BundleId,
                MinDays = t.MinDays,
                MaxDays = t.MaxDays,
                DailyPrice = t.DailyPrice
            })
            .ToListAsync(cancellationToken);

        bundle.Components = await LiveComponents()
            .Where(bi => bi.BundleId == bundleId)
            .OrderBy(bi => bi.CreatedAt).ThenBy(bi => bi.BundleItemId)
            .Select(ToComponentRow)
            .ToListAsync(cancellationToken);

        return bundle;
    }

    // Combo hiển thị: chưa xoá, Active, danh mục Active, có bậc giá và có ít nhất một thành phần còn hiệu lực
    private IQueryable<ProductBundle> VisibleBundles(ProductBundleFilter filter)
    {
        var query = _dbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status == ActiveStatus
                        && !b.Category.IsDeleted && b.Category.Status == ActiveStatus
                        && b.RentalPriceTiers.Any(t => !t.IsDeleted)
                        && b.ProductBundleItems.Any(bi => !bi.IsDeleted
                                                          && !bi.ProductVariant.IsDeleted
                                                          && !bi.ProductVariant.Product.IsDeleted));

        if (filter.CategoryId.HasValue)
        {
            var categoryId = filter.CategoryId.Value;
            query = query.Where(b => b.CategoryId == categoryId);
        }

        if (filter.RestrictToBundleIds is not null)
        {
            var restrictIds = filter.RestrictToBundleIds.ToArray();
            query = query.Where(b => restrictIds.Contains(b.BundleId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(search)
                                     || b.ProductBundleItems.Any(bi => !bi.IsDeleted
                                         && (bi.ProductVariant.Product.Name.ToLower().Contains(search)
                                             || bi.ProductVariant.Name.ToLower().Contains(search)
                                             || bi.ProductVariant.Sku.ToLower().Contains(search))));
        }

        return query;
    }

    private static IQueryable<ProductBundle> Sort(IQueryable<ProductBundle> query, ProductBundleFilter filter)
    {
        var desc = filter.IsDescending;
        return filter.SortBy?.Trim().ToLowerInvariant() switch
        {
            "pricefrom" => desc
                ? query.OrderByDescending(b => b.RentalPriceTiers.Where(t => !t.IsDeleted).Min(t => (decimal?)t.DailyPrice))
                    .ThenBy(b => b.Name).ThenBy(b => b.BundleId)
                : query.OrderBy(b => b.RentalPriceTiers.Where(t => !t.IsDeleted).Min(t => (decimal?)t.DailyPrice))
                    .ThenBy(b => b.Name).ThenBy(b => b.BundleId),
            "createdat" => desc
                ? query.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.BundleId)
                : query.OrderBy(b => b.CreatedAt).ThenBy(b => b.BundleId),
            _ => desc
                ? query.OrderByDescending(b => b.Name).ThenBy(b => b.BundleId)
                : query.OrderBy(b => b.Name).ThenBy(b => b.BundleId)
        };
    }

    // Thành phần còn hiệu lực: dòng chưa xoá, biến thể và sản phẩm chưa xoá
    private IQueryable<ProductBundleItem> LiveComponents()
        => _context.Set<ProductBundleItem>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(bi => !bi.IsDeleted && !bi.ProductVariant.IsDeleted && !bi.ProductVariant.Product.IsDeleted);

    private static readonly Expression<Func<ProductBundleItem, ProductBundleComponentRow>> ToComponentRow =
        bi => new ProductBundleComponentRow
        {
            BundleId = bi.BundleId,
            VariantId = bi.ProductVariantId,
            ProductName = bi.ProductVariant.Product.Name,
            VariantName = bi.ProductVariant.Name,
            Sku = bi.ProductVariant.Sku,
            Quantity = bi.Quantity,
            IsActive = bi.ProductVariant.Status == CatalogStatus.Active
                       && bi.ProductVariant.Product.Status == CatalogStatus.Active,
            PrimaryImageUrl = bi.ProductVariant.Images
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id)
                .Select(i => i.ImageUrl)
                .FirstOrDefault()
        };
}
