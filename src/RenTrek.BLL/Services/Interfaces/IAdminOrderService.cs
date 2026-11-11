using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Danh sách / chi tiết đơn, override participant, thanh toán tay và hủy đơn.</summary>
public interface IAdminOrderService
{
    Task<PagedResponse<AdminOrderResponse>> GetOrdersAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<AdminOrderDetailResponse> GetOrderAsync(string orderCode, CancellationToken cancellationToken);
    Task OverrideParticipantAsync(string orderCode, Guid participantId, ParticipantOverrideRequest request, CancellationToken cancellationToken);
    Task ManualPaymentAsync(string orderCode, ManualPaymentRequest request, CancellationToken cancellationToken);
    Task CancelOrderAsync(string orderCode, ReasonRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Chấp nhận khoản tiền chuyển đối soát (O28): tổng đã nhận đủ và đơn còn giữ chỗ (hoặc đã Confirmed) thì xác nhận đơn, gán Paid
    /// và sinh lệnh hoàn PendingApproval cho phần tiền thừa.
    /// </summary>
    Task ReconcileAcceptAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Hoàn toàn bộ tiền thực nhận của đơn cần đối soát (O28): sinh lệnh hoàn PendingApproval; đơn PendingPayment được giải phóng
    /// giữ chỗ và đóng ở Expired, đơn đã Cancelled / Expired giữ nguyên trạng thái.
    /// </summary>
    Task ReconcileRefundAsync(Guid orderId, CancellationToken cancellationToken);
}
