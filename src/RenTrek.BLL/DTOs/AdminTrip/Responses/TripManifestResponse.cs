namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Danh sách hành khách chính thức (Manifest) của chuyến đi (8.12).
/// </summary>
public sealed class TripManifestResponse
{
    public Guid TripId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalParticipants { get; set; }
    public IReadOnlyList<TripManifestParticipantItemResponse> Participants { get; set; } = [];
}
