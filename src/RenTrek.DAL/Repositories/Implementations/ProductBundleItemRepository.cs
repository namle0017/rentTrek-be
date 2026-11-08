using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class ProductBundleItemRepository : GenericRepository<ProductBundleItem>, IProductBundleItemRepository
{
    public ProductBundleItemRepository(AppDbContext context) : base(context)
    {
    }

    public Task<List<ProductBundleItem>> GetByBundleForUpdateAsync(Guid bundleId, CancellationToken cancellationToken = default)
        => _dbSet.IgnoreQueryFilters()
            .Where(x => x.BundleId == bundleId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
}
