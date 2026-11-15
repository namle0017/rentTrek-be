using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    // Giá trị equipment_assets.asset_status còn trong kho (xem RenTrek.BLL.Constants.EquipmentAssetStatuses)
    private const string AvailableAssetStatus = "Available";

    public ProductRepository(AppDbContext context) : base(context)
    {
    }

    // Đang bán, danh mục đang bật, có biến thể đang bán có bảng giá
    private IQueryable<Product> PublicProducts => _dbSet.AsNoTracking()
        .Where(product => product.Status == CatalogStatus.Active && product.Category.Status == "Active"
            && product.Variants.Any(variant => variant.Status == CatalogStatus.Active
                && variant.RentalPriceTiers.Count > 0));

    public Task<(IReadOnlyList<AdminProductRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminProductFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(product => !product.IsDeleted && !product.Category.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(product => product.Name.ToLower().Contains(search)
                || product.Variants.Any(variant => !variant.IsDeleted && variant.Sku.ToLower().Contains(search)));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(product => product.CategoryId == filter.CategoryId.Value);

        if (filter.Status.HasValue)
            query = query.Where(product => product.Status == filter.Status.Value);

        return query
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Id)
            .ToPageAsync(filter.PageNumber, filter.PageSize, product => new AdminProductRow
            {
                ProductId = product.Id,
                Name = product.Name,
                CategoryName = product.Category.Name,
                PrimaryImageUrl = product.Variants
                    .Where(variant => !variant.IsDeleted)
                    .SelectMany(variant => variant.Images.Where(image => !image.IsDeleted))
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault(),
                VariantsCount = product.Variants.Count(variant => !variant.IsDeleted),
                TotalAssetsCount = product.Variants
                    .Where(variant => !variant.IsDeleted)
                    .SelectMany(variant => variant.EquipmentAssets)
                    .Count(asset => !asset.IsDeleted),
                AvailableAssetsCount = product.Variants
                    .Where(variant => !variant.IsDeleted)
                    .SelectMany(variant => variant.EquipmentAssets)
                    .Count(asset => !asset.IsDeleted && asset.AssetStatus == AvailableAssetStatus),
                Status = product.Status,
                CreatedAt = product.CreatedAt
            }, cancellationToken);
    }

    public Task<Product?> GetAdminDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().AsSplitQuery()
            .Include(product => product.Category)
            .Include(product => product.Variants.Where(variant => !variant.IsDeleted))
                .ThenInclude(variant => variant.Images.Where(image => !image.IsDeleted))
            .Include(product => product.Variants.Where(variant => !variant.IsDeleted))
                .ThenInclude(variant => variant.RentalPriceTiers.Where(tier => !tier.IsDeleted))
            .Include(product => product.Variants.Where(variant => !variant.IsDeleted))
                .ThenInclude(variant => variant.EquipmentAssets.Where(asset => !asset.IsDeleted))
            .SingleOrDefaultAsync(product => product.Id == id && !product.IsDeleted, cancellationToken);

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPublicPagedAsync(
        ProductFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = PublicProducts;
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(product => product.Name.ToLower().Contains(search)
                || (product.Description != null && product.Description.ToLower().Contains(search))
                || product.Variants.Any(variant => variant.Status == CatalogStatus.Active && variant.Sku.ToLower().Contains(search)));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(product => product.CategoryId == filter.CategoryId.Value);

        if (filter.InStockOnly == true)
        {
            query = query.Where(product => product.Variants.Any(variant =>
                variant.Status == CatalogStatus.Active
                && variant.EquipmentAssets.Any(asset => !asset.IsDeleted && asset.AssetStatus == AvailableAssetStatus)));
        }

        if (!string.IsNullOrWhiteSpace(filter.OfferingType))
        {
            var offeringType = filter.OfferingType.Trim();
            if (!string.Equals(offeringType, "Rental", StringComparison.OrdinalIgnoreCase))
                query = query.Where(_ => false);
        }

        if (filter.MinPrice.HasValue)
        {
            var minPrice = filter.MinPrice.Value;
            query = query.Where(product =>
                product.Variants
                    .Where(v => v.Status == CatalogStatus.Active)
                    .SelectMany(v => v.RentalPriceTiers)
                    .Select(t => (decimal?)t.DailyPrice)
                    .Min() >= minPrice);
        }

        if (filter.MaxPrice.HasValue)
        {
            var maxPrice = filter.MaxPrice.Value;
            query = query.Where(product =>
                product.Variants
                    .Where(v => v.Status == CatalogStatus.Active)
                    .SelectMany(v => v.RentalPriceTiers)
                    .Select(t => (decimal?)t.DailyPrice)
                    .Min() <= maxPrice);
        }

        // Giá ngày thấp nhất của các biến thể đang bán; null (không có bậc giá) xếp cuối
        IOrderedQueryable<Product> sorted = (filter.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "name" => filter.IsDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "priceperday" => filter.IsDescending
                ? query.OrderBy(p => p.Variants.Where(v => v.Status == CatalogStatus.Active).SelectMany(v => v.RentalPriceTiers).Select(t => (decimal?)t.DailyPrice).Min() == null)
                    .ThenByDescending(p => p.Variants.Where(v => v.Status == CatalogStatus.Active).SelectMany(v => v.RentalPriceTiers).Select(t => (decimal?)t.DailyPrice).Min())
                : query.OrderBy(p => p.Variants.Where(v => v.Status == CatalogStatus.Active).SelectMany(v => v.RentalPriceTiers).Select(t => (decimal?)t.DailyPrice).Min() == null)
                    .ThenBy(p => p.Variants.Where(v => v.Status == CatalogStatus.Active).SelectMany(v => v.RentalPriceTiers).Select(t => (decimal?)t.DailyPrice).Min()),
            _ => filter.IsDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Max(1, filter.PageSize);
        var total = await query.CountAsync(cancellationToken);
        var items = await WithPublicDetail(sorted.ThenBy(p => p.Id))
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Product?> GetPublicDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => WithPublicDetail(PublicProducts).SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

    public Task<List<Category>> GetPublicCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categoryIds = PublicProducts.Select(product => product.CategoryId);
        return _context.Categories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .OrderBy(category => category.Name).ThenBy(category => category.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Product>> GetRelatedPublicAsync(
        Guid categoryId,
        Guid excludeProductId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 12);
        return WithPublicDetail(PublicProducts
                .Where(product => product.CategoryId == categoryId && product.Id != excludeProductId)
                .OrderByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Id))
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Product> WithPublicDetail(IQueryable<Product> query)
        => query.AsSplitQuery()
            .Include(product => product.Category)
            .Include(product => product.Variants.Where(variant => variant.Status == CatalogStatus.Active))
                .ThenInclude(variant => variant.Images)
            .Include(product => product.Variants.Where(variant => variant.Status == CatalogStatus.Active))
                .ThenInclude(variant => variant.RentalPriceTiers);
}
