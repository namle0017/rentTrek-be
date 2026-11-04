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
}
