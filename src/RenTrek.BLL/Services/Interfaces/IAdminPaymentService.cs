using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Sổ giao dịch (O27), hàng đợi hoàn tiền (O28) và xử lý khoản hoàn theo luồng thủ công
/// (duyệt chuyển kế toán, thử lại, đánh dấu đã hoàn). Không gọi API hoàn tiền của SePay.
/// </summary>
public interface IAdminPaymentService
{
    /// <summary>Danh sách giao dịch thu / hoàn; Staff chỉ thấy đơn trong phạm vi phụ trách.</summary>
    Task<PagedResponse<AdminPaymentListItemResponse>> GetPaymentsAsync(AdminQueryRequest request, CancellationToken cancellationToken);

    /// <summary>Hàng đợi hoàn tiền theo tab PendingApproval | Pending | Failed | NeedsReconciliation (mặc định PendingApproval).</summary>
    Task<PagedResponse<AdminPaymentListItemResponse>> GetRefundsAsync(AdminQueryRequest request, CancellationToken cancellationToken);

    /// <summary>Duyệt khoản hoàn PendingApproval sang Pending (chờ kế toán chuyển khoản).</summary>
    Task<AdminRefundActionResponse> ApproveAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Đưa khoản hoàn Failed về Pending; tối đa 3 lần, sau đó phải hoàn thủ công.</summary>
    Task<AdminRefundActionResponse> RetryAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Ghi nhận khoản hoàn Pending / Failed đã chuyển khoản thủ công (Succeeded) và cập nhật trạng thái thanh toán của đơn.</summary>
    Task<AdminRefundActionResponse> MarkManualAsync(Guid paymentId, MarkManualRefundRequest request, CancellationToken cancellationToken);
}
