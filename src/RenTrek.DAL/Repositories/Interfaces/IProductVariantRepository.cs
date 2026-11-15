using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IProductVariantRepository : IGenericRepository<ProductVariant>
{
    /// <summary>
    /// Thông tin biến thể để dựng dòng thuê. Trả cả biến thể đã xoá / ngừng bán với <c>IsActive = false</c>
    /// (bỏ qua query filter); id không tồn tại thì không có trong kết quả.
    /// </summary>
    Task<List<RentalVariantInfo>> GetActiveForRentalAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default);

    /// <summary>Biến thể và sản phẩm chưa xoá, kèm Product, sắp theo tên sản phẩm rồi tên biến thể. Không tracking.</summary>
    Task<List<ProductVariant>> GetOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// SKU đã có ở biến thể khác chưa (tính cả biến thể đã xoá vì unique index phủ mọi dòng), so không phân biệt hoa thường.
    /// </summary>
    Task<bool> SkuExistsAsync(string sku, Guid? excludeVariantId, CancellationToken cancellationToken = default);

    /// <summary>Giá trị thiết bị (đền bù mất) theo biến thể, kể cả biến thể đã xoá mềm; null nếu chưa nhập.</summary>
    Task<Dictionary<Guid, decimal?>> GetReplacementValuesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>Danh sách biến thể chưa xoá của một sản phẩm (có tracking để cập nhật).</summary>
    Task<List<ProductVariant>> GetByProductForUpdateAsync(Guid productId, CancellationToken cancellationToken = default);
}
