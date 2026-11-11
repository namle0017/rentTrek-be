using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Cấu hình hệ thống, ẩn / hiện đánh giá và nhật ký audit.</summary>
public interface IAdminGovernanceService
{
    Task<List<AdminSettingResponse>> GetSettingsAsync(CancellationToken cancellationToken);
    Task UpdateSettingAsync(string key, UpdateSettingRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<AdminReviewResponse>> GetReviewsAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task SetReviewVisibilityAsync(Guid id, bool hidden, string? reason, CancellationToken cancellationToken);
    Task<PagedResponse<AdminAuditResponse>> GetAuditLogsAsync(AdminQueryRequest request, Guid? userId, CancellationToken cancellationToken);
}
