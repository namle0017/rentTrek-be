using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>Danh sách sản phẩm cho vận hành, mới tạo trước.</summary>
    Task<(IReadOnlyList<AdminProductRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminProductFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sản phẩm chưa xoá kèm Category, biến thể chưa xoá (ảnh chưa xoá, bậc giá chưa xoá, thiết bị chưa xoá).
    /// Không tracking.
    /// </summary>
    Task<Product?> GetAdminDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Một trang sản phẩm công khai kèm Category và biến thể đang bán (ảnh, bậc giá). Không tracking.
    /// </summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPublicPagedAsync(
        ProductFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Sản phẩm công khai kèm Category và biến thể đang bán (ảnh, bậc giá). Không tracking.</summary>
    Task<Product?> GetPublicDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Danh mục có ít nhất một sản phẩm công khai, sắp theo tên.</summary>
    Task<List<Category>> GetPublicCategoriesAsync(CancellationToken cancellationToken = default);
}
