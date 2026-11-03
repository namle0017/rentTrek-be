using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalOps.Requests;
using RenTrek.BLL.DTOs.RentalOps.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Vận hành đơn thuê cho Staff/Admin: giao, nhận trả, phí phát sinh, đóng đơn (FR-R06–R08, FR-C02–C05).</summary>
public interface IRentalFulfillmentService
{
    Task<PagedResponse<AdminRentalOrderListItemResponse>> GetListAsync(AdminRentalOrderQueryRequest query, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> HandoverAsync(Guid orderId, HandoverRentalRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> ReturnAsync(Guid orderId, ReturnRentalAssetsRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> CreateChargeAsync(Guid orderId, CreateRentalChargeRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> AssessChargeAsync(Guid orderId, Guid chargeId, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> VoidChargeAsync(Guid orderId, Guid chargeId, VoidRentalChargeRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> CompleteAsync(Guid orderId, CancellationToken cancellationToken = default);
}
