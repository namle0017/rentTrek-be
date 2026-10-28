using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IPromotionRepository : IGenericRepository<Promotion>
{
    /// <summary>
    /// Khuyến mãi theo mã (không phân biệt hoa thường, chưa xoá) kèm các bậc chưa xoá; null nếu không có.
    /// Entity chỉ đọc (AsNoTracking).
    /// </summary>
    Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

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
}
