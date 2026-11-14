using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalReport.Requests;
using RenTrek.BLL.DTOs.RentalReport.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Báo cáo đơn thuê gửi admin. Duyệt ReturnInspection = chốt phí nháp + quyết toán cọc; duyệt SuspectedLoss = xác nhận mất
/// (bồi thường giá trị thiết bị) + quyết toán; duyệt ExtraPayment = ghi nhận đã thu ngoài hệ thống; NoShow / LateCancellation chỉ xác nhận đã xem.
/// </summary>
public interface IRentalReportService
{
    Task<RentalReportResponse> SubmitReturnInspectionAsync(Guid orderId, SubmitReturnInspectionRequest request, CancellationToken cancellationToken = default);

    Task<PagedResponse<RentalReportResponse>> SearchAsync(RentalReportQueryRequest query, CancellationToken cancellationToken = default);

    Task<RentalReportResponse> GetAsync(Guid reportId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalReportResponse>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<RentalReportResponse> ApproveAsync(Guid reportId, ReviewRentalReportRequest request, CancellationToken cancellationToken = default);

    Task<RentalReportResponse> RejectAsync(Guid reportId, ReviewRentalReportRequest request, CancellationToken cancellationToken = default);
}
