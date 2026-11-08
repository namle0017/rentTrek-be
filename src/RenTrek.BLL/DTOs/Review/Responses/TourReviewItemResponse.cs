namespace RenTrek.BLL.DTOs.Review.Responses;

/// <summary>
/// Một đánh giá công khai hiển thị trên trang chi tiết tour.
/// </summary>
public sealed class TourReviewItemResponse
{
    public Guid ReviewId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public DateTimeOffset TripStartAt { get; set; }
}
