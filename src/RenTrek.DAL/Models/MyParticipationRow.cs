namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng kết quả tham gia chuyến đi của người dùng hiện tại được chiếu trực tiếp từ DB.
/// </summary>
public class MyParticipationRow
{
    public Guid ParticipantId { get; set; }
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string TripStatus { get; set; } = string.Empty;
    public string ParticipantStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string OrderPaymentStatus { get; set; } = string.Empty;
    public int UnreadPostCount { get; set; }
    public bool HasReview { get; set; }
    /// <summary>Mã đơn đặt tour chứa lượt tham gia, dùng để liên kết sang trang booking.</summary>
    public Guid OrderId { get; set; }
    public Guid? ReviewId { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
}
