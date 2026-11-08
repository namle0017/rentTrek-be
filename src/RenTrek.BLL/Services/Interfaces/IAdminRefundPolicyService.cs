using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Quản lý chính sách hoàn tiền có phiên bản (03-API-CONTRACT.md §20, FR-F05).
/// </summary>
public interface IAdminRefundPolicyService
{
    /// <summary>Danh sách chính sách, lọc theo loại (Tour / Rental) nếu có.</summary>
    Task<IReadOnlyList<RefundPolicyListItemResponse>> GetPoliciesAsync(string? policyType, CancellationToken cancellationToken);

    /// <summary>Chi tiết chính sách kèm bảng bậc và cờ khóa.</summary>
    Task<RefundPolicyDetailResponse> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken);

    /// <summary>Tạo họ chính sách mới, phiên bản 1, chưa có bậc.</summary>
    Task<Guid> CreatePolicyAsync(SaveRefundPolicyRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa thông tin chính sách; chặn khi đã có chuyến/đơn dùng.</summary>
    Task UpdatePolicyAsync(Guid policyId, SaveRefundPolicyRequest request, CancellationToken cancellationToken);

    /// <summary>Thay toàn bộ bảng bậc; chặn khi đã có chuyến/đơn dùng.</summary>
    Task SetRulesAsync(Guid policyId, RefundPolicyRulesRequest request, CancellationToken cancellationToken);

    /// <summary>Đóng hiệu lực bản cũ và tạo phiên bản kế tiếp cùng họ, sao chép bảng bậc. Trả id bản mới.</summary>
    Task<Guid> CloneVersionAsync(Guid policyId, CancellationToken cancellationToken);
}
