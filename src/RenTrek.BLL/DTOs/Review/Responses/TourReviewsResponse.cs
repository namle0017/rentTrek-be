using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.DTOs.Review.Responses;

/// <summary>
/// Tổng hợp điểm trung bình, phân bố số sao và danh sách phân trang đánh giá theo họ tour.
/// </summary>
public sealed class TourReviewsResponse
{
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public Dictionary<int, int> Distribution { get; set; } = [];
    public PagedResponse<TourReviewItemResponse> Reviews { get; set; } = new();
}
