using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRentalPriceTierRepository : IGenericRepository<RentalPriceTier>
{
    /// <summary>Các bậc giá chưa xoá của danh sách biến thể (sắp theo MinDays).</summary>
    Task<List<RentalPriceTierRow>> GetForVariantsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default);

    /// <summary>Các bậc giá chưa xoá của danh sách combo (sắp theo MinDays).</summary>
    Task<List<RentalPriceTierRow>> GetForBundlesAsync(
        IReadOnlyCollection<Guid> bundleIds,
        CancellationToken cancellationToken = default);

    /// <summary>Các bậc giá chưa xoá của đúng một biến thể hoặc một combo, có tracking để thay thế.</summary>
    Task<List<RentalPriceTier>> GetForUpdateAsync(Guid? variantId, Guid? bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bảng giá tổng hợp: mọi bậc giá chưa xoá của biến thể (sản phẩm, danh mục chưa xoá) và combo chưa xoá,
    /// lọc theo danh mục nếu có; sắp theo danh mục, tên, MinDays.
    /// </summary>
    Task<List<PricingTierRow>> GetPricingOverviewAsync(Guid? categoryId, CancellationToken cancellationToken = default);
}
