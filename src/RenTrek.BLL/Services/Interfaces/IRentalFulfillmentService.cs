using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalOps.Requests;
using RenTrek.BLL.DTOs.RentalOps.Responses;
using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Vận hành đơn thuê cho Staff/Admin: giao, nhận trả, phí phát sinh, đóng đơn (FR-R06–R08, FR-C02–C05).</summary>
public interface IRentalFulfillmentService
{
    Task<PagedResponse<AdminRentalOrderListItemResponse>> GetListAsync(AdminRentalOrderQueryRequest query, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> ClaimAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> ScanHandoverAsync(Guid orderId, ScanHandoverRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> HandoverAsync(Guid orderId, HandoverRentalRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> ReturnAsync(Guid orderId, ReturnRentalAssetsRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> CreateChargeAsync(Guid orderId, CreateRentalChargeRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> AssessChargeAsync(Guid orderId, Guid chargeId, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> VoidChargeAsync(Guid orderId, Guid chargeId, VoidRentalChargeRequest request, CancellationToken cancellationToken = default);
    Task<AdminRentalOrderDetailResponse> CompleteAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách thiết bị cần giao theo ngày giờ Việt Nam.</summary>
    Task<AdminRentalScheduleResponse> GetHandoverListAsync(DateOnly? targetDate, CancellationToken cancellationToken = default);

    /// <summary>Xuất CSV danh sách thiết bị cần giao theo ngày giờ Việt Nam.</summary>
    Task<(byte[] FileBytes, string FileName)> ExportHandoverListCsvAsync(DateOnly? targetDate, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách thiết bị cần nhận lại theo ngày giờ Việt Nam.</summary>
    Task<AdminRentalScheduleResponse> GetReturnListAsync(DateOnly? targetDate, CancellationToken cancellationToken = default);

    /// <summary>Xuất CSV danh sách thiết bị cần nhận lại theo ngày giờ Việt Nam.</summary>
    Task<(byte[] FileBytes, string FileName)> ExportReturnListCsvAsync(DateOnly? targetDate, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử điều chỉnh (gia hạn) của đơn thuê cho màn vận hành; 404 ORDER_NOT_FOUND nếu không có đơn.</summary>
    Task<List<RentalOrderAdjustmentResponse>> GetAdjustmentsAsync(Guid orderId, CancellationToken cancellationToken = default);
}
