using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalReport.Requests;
using RenTrek.BLL.DTOs.RentalReport.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Ảnh bằng chứng giao / nhận / hư hỏng / khách báo lỗi.</summary>
public interface IRentalEvidenceService
{
    /// <summary>Staff tải ảnh (Handover / Return / Damage).</summary>
    Task<RenTrek.BLL.DTOs.RentalOrder.Responses.RentalEvidencePhotoResponse> UploadStaffAsync(
        Guid orderId, string stage, Guid? assignmentId, string? note,
        Stream content, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default);

    /// <summary>Khách tải ảnh khi báo lỗi lúc nhận đồ (CustomerIssue).</summary>
    Task<RenTrek.BLL.DTOs.RentalOrder.Responses.RentalEvidencePhotoResponse> UploadCustomerAsync(
        Guid userId, Guid orderId, Guid? assignmentId, string? note,
        Stream content, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default);

    /// <summary>Xoá ảnh chụp nhầm khi bước tương ứng chưa hoàn tất.</summary>
    Task DeleteAsync(Guid orderId, Guid photoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RenTrek.BLL.DTOs.RentalOrder.Responses.RentalEvidencePhotoResponse>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
