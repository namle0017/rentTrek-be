using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefundPolicyRepository : IGenericRepository<RefundPolicy>
{
    /// <summary>
    /// Chính sách hoàn tiền hiện hành: chưa xoá, đúng loại và trạng thái, có hiệu lực tại <paramref name="nowUtc"/>
    /// (EffectiveFrom &lt;= now &lt; EffectiveUntil), lấy phiên bản cao nhất. Kèm các rule chưa xoá sắp theo
    /// MinHoursBefore giảm dần. Entity chỉ đọc (AsNoTracking); null nếu không có.
    /// </summary>
    Task<RefundPolicy?> GetCurrentAsync(
        string policyType,
        string status,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
