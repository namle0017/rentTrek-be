namespace RenTrek.BLL.DTOs.Review.Requests;

/// <summary>
/// Dữ liệu tạo mới hoặc chỉnh sửa đánh giá chuyến đi từ hành khách.
/// </summary>
public sealed class SaveReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
