namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng đánh giá công khai của họ tour đọc từ cơ sở dữ liệu.
/// </summary>
public sealed class TourReviewRow
{
    public Guid ReviewId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public DateTimeOffset TripStartAt { get; set; }
}
