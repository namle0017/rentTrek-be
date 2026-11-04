using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;

namespace RenTrek.BLL.Services.Warehouse.Interfaces;

public interface IFulfillmentService
{
    Task<HandoverChecklistResponse> GetHandoverChecklistAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<HandoverScanResultResponse> ScanHandoverAssetAsync(string orderCode, HandoverScanRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteHandoverAsync(string orderCode, CompleteHandoverRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<ReturnChecklistResponse> GetReturnChecklistAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<ReturnScanResultResponse> ScanReturnAssetAsync(string orderCode, ReturnScanRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> ReportLostAssetAsync(string orderCode, ReportLostAssetRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteReturnAsync(string orderCode, CancellationToken cancellationToken = default);

    Task<List<RentalChargeDetailResponse>> GetChargesAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<RentalChargeDetailResponse> CreateChargeAsync(string orderCode, CreateRentalChargeRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> AssessChargeAsync(Guid chargeId, CancellationToken cancellationToken = default);
    Task<bool> VoidChargeAsync(Guid chargeId, VoidRentalChargeRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteRentalOrderAsync(string orderCode, CancellationToken cancellationToken = default);
}
