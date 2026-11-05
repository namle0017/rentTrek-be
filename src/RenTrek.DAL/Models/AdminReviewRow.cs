namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng đánh giá ở trang quản trị, kèm người đánh giá, tour, chuyến và hướng dẫn viên.
/// </summary>
public class AdminReviewRow
{
    public Guid ReviewId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string CustomerFullName { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset TripStartAt { get; set; }

    /// <summary>Hướng dẫn viên của nhóm; null nếu người đánh giá chưa được xếp nhóm.</summary>
    public string? GuideFullName { get; set; }

    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
