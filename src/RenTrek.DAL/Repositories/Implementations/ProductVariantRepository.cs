using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho biến thể sản phẩm qua EF Core.
/// </summary>
public class ProductVariantRepository : GenericRepository<ProductVariant>, IProductVariantRepository
{
    public ProductVariantRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<RentalVariantInfo>> GetActiveForRentalAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default)
    {
        if (variantIds.Count == 0)
            return [];

        var ids = variantIds.Distinct().ToArray();
        return await _dbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new RentalVariantInfo
            {
                VariantId = v.Id,
                ProductId = v.ProductId,
                ProductName = v.Product.Name,
                VariantName = v.Name,
                Sku = v.Sku,
                IsActive = !v.IsDeleted && v.Status == CatalogStatus.Active
                           && !v.Product.IsDeleted && v.Product.Status == CatalogStatus.Active
            })
            .ToListAsync(cancellationToken);
    }
}
