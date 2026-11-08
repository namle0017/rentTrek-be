using RenTrek.BLL.DTOs.Review.Requests;
using RenTrek.BLL.DTOs.Review.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Nghiệp vụ gửi, chỉnh sửa đánh giá sau chuyến đi và xem đánh giá công khai theo họ tour (FR-V04, FR-V05, FR-V06).
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Hành khách gửi đánh giá đầu tiên cho chuyến đi đã hoàn thành và thanh toán hợp lệ.
    /// </summary>
    Task<ParticipantReviewResponse> CreateReviewAsync(
        Guid participantId,
        SaveReviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hành khách chỉnh sửa đánh giá của mình trong thời gian cho phép kể từ khi tạo.
    /// </summary>
    Task<ParticipantReviewResponse> UpdateReviewAsync(
        Guid participantId,
        SaveReviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thống kê và danh sách đánh giá công khai đã gộp theo họ tour, loại trừ đánh giá bị ẩn hoặc đã xóa.
    /// </summary>
    Task<TourReviewsResponse> GetTourReviewsAsync(
        Guid tourId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}
