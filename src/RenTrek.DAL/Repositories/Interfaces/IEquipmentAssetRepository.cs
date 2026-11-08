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

    /// <summary>
    /// Id các tài sản trong <paramref name="equipmentAssetIds"/> đang bị một phân bổ khác chặn trong khoảng
    /// [WindowStart, WindowEnd) của bộ lọc, cùng tập trạng thái chặn và quy tắc Issued quá hạn chưa trả như truy vấn khả dụng.
    /// Bỏ qua các phân bổ có id thuộc <paramref name="excludedAssignmentIds"/> (phân bổ của chính đơn đang gia hạn).
    /// </summary>
    Task<List<Guid>> GetBlockedAssetIdsAsync(
        IReadOnlyCollection<Guid> equipmentAssetIds,
        IReadOnlyCollection<Guid> excludedAssignmentIds,
        AssetAvailabilityFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thiết bị phân trang kèm thông tin variant, product và assignment hiện tại (Màn O20).
    /// </summary>
    Task<(IReadOnlyList<EquipmentAsset> Items, int TotalCount)> GetPagedAssetsAsync(
        string? search,
        Guid? productVariantId,
        string? assetStatus,
        int pageNumber,
        int pageSize,
        string sortBy,
        bool isDescending,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết thiết bị theo mã asset_code kèm variant, assignments và charges.
    /// </summary>
    Task<EquipmentAsset?> GetByCodeWithDetailsAsync(string assetCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết thiết bị theo Guid Id kèm variant, assignments và charges.
    /// </summary>
    Task<EquipmentAsset?> GetByIdWithDetailsAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm mã tiếp theo theo prefix (tự động tăng số thứ tự 4 chữ số, vd: TENT-4P-0001).
    /// </summary>
    Task<string> GenerateNextAssetCodeAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thiết bị và các phân bổ phục vụ biểu đồ Gantt trong khoảng [from, to] (Màn O21).
    /// </summary>
    Task<IReadOnlyList<EquipmentAsset>> GetAssetsForGanttAsync(
        DateTime from,
        DateTime to,
        Guid? categoryId,
        Guid? productVariantId,
        CancellationToken cancellationToken = default);
}
