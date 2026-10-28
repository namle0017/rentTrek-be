using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IEquipmentAssetRepository : IGenericRepository<EquipmentAsset>
{
    /// <summary>
    /// Số tài sản khả dụng theo từng biến thể (biến thể không có tài sản khả dụng không xuất hiện trong kết quả = 0).
    /// Khả dụng: chưa xoá, asset_status cho thuê được, không có phân bổ chặn giao với khoảng
    /// [WindowStart, WindowEnd), và không dính tài sản đã giao quá hạn chưa trả (BR-20).
    /// </summary>
    Task<Dictionary<Guid, int>> GetAvailableCountsAsync(
        IReadOnlyCollection<Guid> variantIds,
        AssetAvailabilityFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Id tài sản khả dụng của một biến thể, thứ tự xác định (asset_code, id), tối đa <paramref name="take"/> phần tử.
    /// Gọi trong transaction tạo đơn sau khi đã <see cref="AcquireVariantLocksAsync"/>.
    /// </summary>
    Task<List<Guid>> GetAvailableAssetIdsAsync(
        Guid variantId,
        AssetAvailabilityFilter filter,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá advisory theo transaction cho từng biến thể (khoá <c>rental:variant:{id:N}</c>), theo thứ tự id tăng dần
    /// để tránh deadlock. BẮT BUỘC gọi trong transaction đã mở; khoá tự nhả khi commit / rollback.
    /// </summary>
    Task AcquireVariantLocksAsync(IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default);
}
