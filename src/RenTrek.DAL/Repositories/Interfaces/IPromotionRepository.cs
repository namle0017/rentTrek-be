using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IPromotionRepository : IGenericRepository<Promotion>
{
    /// <summary>
    /// Khuyến mãi theo mã (không phân biệt hoa thường, chưa xoá) kèm các bậc chưa xoá; null nếu không có.
    /// Entity chỉ đọc (AsNoTracking).
    /// </summary>
    Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khuyến mãi có mã, chưa xoá, đang trong thời gian hiệu lực tại <paramref name="nowUtc"/>, kèm các bậc chưa xoá.
    /// Entity chỉ đọc (AsNoTracking). Chưa lọc trạng thái / đối tượng áp dụng.
    /// </summary>
    Task<List<Promotion>> GetRunningWithCodeAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Số đơn (distinct) đã dùng khuyến mãi với applied_promotions.status thuộc <paramref name="countedStatuses"/>;
    /// khi <paramref name="customerUserId"/> có giá trị thì chỉ đếm đơn của khách đó. Bỏ qua đơn đã xoá.
    /// </summary>
    Task<int> CountUsageAsync(
        Guid promotionId,
        IReadOnlyCollection<string> countedStatuses,
        Guid? customerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá advisory theo transaction cho khuyến mãi (khoá <c>rental:promotion:{id:N}</c>) để kiểm tra lượt dùng
    /// không bị vượt khi đặt đơn đồng thời. BẮT BUỘC gọi trong transaction đã mở.
    /// </summary>
    Task AcquireLockAsync(Guid promotionId, CancellationToken cancellationToken = default);

    /// <summary>Danh sách khuyến mãi chưa xoá trang quản trị.</summary>
    Task<(IReadOnlyList<AdminPromotionRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminPromotionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Một khuyến mãi chưa xoá (chỉ đọc) kèm số đơn đã dùng; null nếu không có.</summary>
    Task<AdminPromotionRow?> GetAdminRowAsync(Guid promotionId, CancellationToken cancellationToken = default);

    /// <summary>Có khuyến mãi khác (kể cả đã xoá) đang dùng mã này.</summary>
    Task<bool> CodeExistsAsync(string code, Guid exceptPromotionId, CancellationToken cancellationToken = default);

    /// <summary>Khuyến mãi chưa xoá (tracking).</summary>
    Task<Promotion?> GetForUpdateAsync(Guid promotionId, CancellationToken cancellationToken = default);

    /// <summary>Các bậc chưa xoá của khuyến mãi (tracking), tăng dần theo ngưỡng.</summary>
    Task<List<PromotionTier>> GetTiersAsync(Guid promotionId, CancellationToken cancellationToken = default);

    /// <summary>Số biến thể chưa xoá trong <paramref name="productVariantIds"/> (kiểm tra quà tặng).</summary>
    Task<int> CountActiveVariantsAsync(IReadOnlyCollection<Guid> productVariantIds, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử áp dụng khuyến mãi vào đơn (mọi bậc).</summary>
    Task<(IReadOnlyList<PromotionUsageRow> Items, int TotalCount)> GetUsagePagedAsync(
        PromotionUsageFilter filter, CancellationToken cancellationToken = default);
}
