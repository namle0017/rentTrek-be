using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IProductBundleItemRepository : IGenericRepository<ProductBundleItem>
{
    /// <summary>Thành phần chưa xoá của một combo, theo thứ tự tạo, có tracking để thay thế.</summary>
    Task<List<ProductBundleItem>> GetByBundleForUpdateAsync(Guid bundleId, CancellationToken cancellationToken = default);
}
