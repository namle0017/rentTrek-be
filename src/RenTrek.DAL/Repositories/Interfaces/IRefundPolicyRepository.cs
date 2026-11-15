using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefundPolicyRepository : IGenericRepository<RefundPolicy>
{
    /// <summary>
    /// Chính sách hoàn tiền hiện hành: chưa xoá, đúng loại và trạng thái, có hiệu lực tại <paramref name="nowUtc"/>
    /// (EffectiveFrom &lt;= now &lt; EffectiveUntil), lấy bản có EffectiveFrom mới nhất rồi Version cao nhất. Kèm các rule
    /// chưa xoá sắp theo MinHoursBefore giảm dần. Entity chỉ đọc (AsNoTracking); null nếu không có.
    /// </summary>
    Task<RefundPolicy?> GetCurrentAsync(
        string policyType,
        string status,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Trang chính sách chưa xoá cho admin: lọc, sắp xếp, phân trang kèm số bậc / chuyến / đơn / phiên bản.</summary>
    Task<(IReadOnlyList<AdminRefundPolicyRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminRefundPolicyFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Một dòng admin theo id (chưa xoá); null nếu không có.</summary>
    Task<AdminRefundPolicyRow?> GetAdminRowAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Mọi phiên bản chưa xoá của một họ chính sách, phiên bản giảm dần.</summary>
    Task<List<AdminRefundPolicyRow>> GetFamilyVersionsAsync(Guid policyFamilyId, CancellationToken cancellationToken = default);

    /// <summary>Số chuyến và số đơn đang tham chiếu chính sách.</summary>
    Task<(int Trips, int Orders)> GetUsageCountsAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Số chính sách chưa xoá, Active và đang có hiệu lực tại <paramref name="nowUtc"/> của một loại,
    /// không tính <paramref name="excludePolicyId"/>.
    /// </summary>
    Task<int> CountEffectiveActiveAsync(
        string policyType, DateTime nowUtc, Guid? excludePolicyId, CancellationToken cancellationToken = default);

    /// <summary>true nếu không còn phiên bản chưa xoá nào có Version lớn hơn trong họ.</summary>
    Task<bool> IsLatestVersionAsync(Guid policyFamilyId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Các chính sách Active (tracked) đang hiệu lực tại <paramref name="effectiveFrom"/> cần đóng khi kích hoạt bản mới:
    /// cùng họ (trừ <paramref name="excludePolicyId"/>) và, nếu <paramref name="includeOtherFamilies"/> và loại Rental,
    /// cả các họ Rental khác.
    /// </summary>
    Task<List<RefundPolicy>> GetOpenForActivationAsync(
        Guid policyFamilyId,
        string policyType,
        Guid excludePolicyId,
        DateTime effectiveFrom,
        bool includeOtherFamilies,
        CancellationToken cancellationToken = default);

    /// <summary>Chính sách chưa xoá (tracked) để sửa; null nếu không có.</summary>
    Task<RefundPolicy?> GetForUpdateAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Chính sách chưa xoá kèm rule chưa xoá sắp theo MinHoursBefore giảm dần (chỉ đọc).</summary>
    Task<RefundPolicy?> GetDetailAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>true nếu đã có chuyến đi hoặc đơn hàng tham chiếu chính sách này.</summary>
    Task<bool> IsInUseAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Phiên bản lớn nhất trong một họ chính sách (kể cả bản đã xoá, để không trùng khóa duy nhất).</summary>
    Task<int> GetMaxVersionAsync(Guid policyFamilyId, CancellationToken cancellationToken = default);
}
