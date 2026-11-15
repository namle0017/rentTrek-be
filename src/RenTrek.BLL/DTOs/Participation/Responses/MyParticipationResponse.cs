namespace RenTrek.BLL.DTOs.Participation.Responses;

/// <summary>
/// Thông tin một lượt tham gia chuyến đi đã liên kết tài khoản, phục vụ danh sách chuyến đi của người tham gia.
/// </summary>
public sealed class MyParticipationResponse
{
    public Guid ParticipantId { get; set; }
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string TripStatus { get; set; } = string.Empty;
    public int UnreadPostCount { get; set; }
    public bool CanReview { get; set; }
    public bool HasReview { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ReviewId { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
}
