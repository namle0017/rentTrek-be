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
}
