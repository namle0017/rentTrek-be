using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Quản lý chính sách hoàn tiền có phiên bản (03-API-CONTRACT.md §20, FR-F05).
/// </summary>
public interface IAdminRefundPolicyService
{
    /// <summary>Danh sách chính sách phân trang: lọc theo loại, trạng thái hiển thị, tên; mặc định chỉ phiên bản mới nhất.</summary>
    Task<PagedResponse<RefundPolicyListItemResponse>> GetPoliciesAsync(RefundPolicyQueryRequest request, CancellationToken cancellationToken);

    /// <summary>Chi tiết chính sách kèm bảng bậc, lịch sử phiên bản và các cờ thao tác.</summary>
    Task<RefundPolicyDetailResponse> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken);

    /// <summary>Tạo họ chính sách mới (phiên bản 1) cùng bảng bậc trong một transaction; Draft hoặc Active ngay.</summary>
    Task<Guid> CreatePolicyAsync(SaveRefundPolicyRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa thông tin và thay bảng bậc; 409 POLICY_ALREADY_IN_USE khi đã có chuyến/đơn dùng.</summary>
    Task UpdatePolicyAsync(Guid policyId, SaveRefundPolicyRequest request, CancellationToken cancellationToken);

    /// <summary>Thay toàn bộ bảng bậc; 409 POLICY_ALREADY_IN_USE khi đã có chuyến/đơn dùng.</summary>
    Task SetRulesAsync(Guid policyId, RefundPolicyRulesRequest request, CancellationToken cancellationToken);

    /// <summary>Tạo bản nháp phiên bản kế tiếp cùng họ, sao chép bảng bậc; bản gốc giữ nguyên. Trả id bản mới.</summary>
    Task<Guid> CloneVersionAsync(Guid policyId, CancellationToken cancellationToken);

    /// <summary>Kích hoạt bản nháp (hoặc bản Archived mới nhất của họ) và đóng các bản cũ đang hiệu lực.</summary>
    Task ActivateAsync(Guid policyId, CancellationToken cancellationToken);

    /// <summary>Ngừng áp dụng chính sách Active; 409 REFUND_POLICY_LAST_ACTIVE nếu là bản hiệu lực duy nhất của loại.</summary>
    Task ArchiveAsync(Guid policyId, CancellationToken cancellationToken);

    /// <summary>Xoá mềm chính sách và bảng bậc; 409 nếu đã có chuyến/đơn dùng hoặc là bản hiệu lực duy nhất của loại.</summary>
    Task DeletePolicyAsync(Guid policyId, CancellationToken cancellationToken);
}
