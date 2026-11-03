using RenTrek.BLL.DTOs.TripBooking.Responses;

namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Chi tiết một TripGroup được giao cho hướng dẫn viên (gồm cả Draft và Published).
/// </summary>
public sealed class GuideTripGroupResponse
{
    public Guid TripGroupId { get; set; }
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TripStatus { get; set; } = string.Empty;
    public DateTimeOffset TripStartAt { get; set; }
    public DateTimeOffset TripEndAt { get; set; }
    public int CurrentParticipants { get; set; }
    public int MaxParticipants { get; set; }
    public IReadOnlyList<GuideGroupParticipantResponse> Participants { get; set; } = [];
    public IReadOnlyList<TripActivityPreviewResponse> Activities { get; set; } = [];
}
