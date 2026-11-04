using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Dashboard, cảnh báo, KPI và các báo cáo vận hành.</summary>
public interface IAdminReportService
{
    Task<AdminDashboardStatsResponse> GetStatsAsync(CancellationToken cancellationToken);
    Task<List<AdminAlertResponse>> GetAlertsAsync(CancellationToken cancellationToken);
    Task<AdminKpisResponse> GetKpisAsync(CancellationToken cancellationToken);
    Task<List<AdminRevenueResponse>> GetRevenueAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<List<AdminOccupancyResponse>> GetOccupancyAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<List<AdminUtilizationResponse>> GetUtilizationAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<AdminRefundChargeResponse> GetRefundChargesAsync(AdminQueryRequest request, CancellationToken cancellationToken);
}
