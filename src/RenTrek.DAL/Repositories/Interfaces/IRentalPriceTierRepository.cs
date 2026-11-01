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
}
