namespace RenTrek.BLL.DTOs.Review.Responses;

/// <summary>
/// Thông tin đánh giá chuyến đi do hành khách tạo hoặc cập nhật.
/// </summary>
public sealed class ParticipantReviewResponse
{
    public Guid ReviewId { get; set; }
    public Guid ParticipantId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
