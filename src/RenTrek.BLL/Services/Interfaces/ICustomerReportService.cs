using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.CustomerReport.Requests;
using RenTrek.BLL.DTOs.CustomerReport.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Báo cáo khách gửi về đơn thuê (sự cố thiết bị) hoặc chuyến đi. Khách gửi / xem báo cáo của mình;
/// Admin / Staff xem toàn bộ và cập nhật trạng thái (Open → InProgress → Resolved | Rejected), khách được thông báo.
/// </summary>
public interface ICustomerReportService
{
    Task<CustomerReportImageResponse> UploadImageAsync(
        Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default);

    Task<CustomerReportResponse> CreateAsync(CreateCustomerReportRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerReportResponse>> GetMineAsync(Guid? orderId, CancellationToken cancellationToken = default);

    Task<PagedResponse<CustomerReportResponse>> SearchAsync(CustomerReportQueryRequest query, CancellationToken cancellationToken = default);

    Task<CustomerReportSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<CustomerReportResponse> GetAsync(Guid customerReportId, CancellationToken cancellationToken = default);

    Task<CustomerReportResponse> UpdateStatusAsync(
        Guid customerReportId, UpdateCustomerReportStatusRequest request, CancellationToken cancellationToken = default);
}
