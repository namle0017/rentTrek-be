namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Thẻ chuyến đi của Hướng dẫn viên tại màn G01 (24.1) — tuyệt đối không chứa dữ liệu tài chính.
/// </summary>
public sealed class GuideTripCardResponse
{
    public Guid TripId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string TripStatus { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public int MaxParticipants { get; set; }
    public GuideMeetingLocationResponse MeetingLocation { get; set; } = new();
}
