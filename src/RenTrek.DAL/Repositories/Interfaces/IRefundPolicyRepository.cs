using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

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

    /// <summary>Mọi chính sách chưa xoá, sắp theo họ chính sách rồi phiên bản giảm dần; lọc theo loại nếu có.</summary>
    Task<List<AdminRefundPolicyRow>> GetAdminListAsync(string? policyType, CancellationToken cancellationToken = default);

    /// <summary>Chính sách chưa xoá (tracked) để sửa; null nếu không có.</summary>
    Task<RefundPolicy?> GetForUpdateAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Chính sách chưa xoá kèm rule chưa xoá sắp theo MinHoursBefore giảm dần (chỉ đọc).</summary>
    Task<RefundPolicy?> GetDetailAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>true nếu đã có chuyến đi hoặc đơn hàng tham chiếu chính sách này.</summary>
    Task<bool> IsInUseAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Phiên bản lớn nhất trong một họ chính sách (kể cả bản đã xoá, để không trùng khóa duy nhất).</summary>
    Task<int> GetMaxVersionAsync(Guid policyFamilyId, CancellationToken cancellationToken = default);
}
