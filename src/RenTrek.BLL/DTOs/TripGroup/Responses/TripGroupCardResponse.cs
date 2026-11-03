namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Thẻ một TripGroup trong snapshot chia đoàn của Staff/Admin/Guide.
/// </summary>
public sealed class TripGroupCardResponse
{
    public Guid TripGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public TripGroupGuideSummaryResponse? Guide { get; set; }
    public int MaxParticipants { get; set; }
    public int OverflowSlots { get; set; }
    public int ParticipantCount { get; set; }
    public int RemainingStandardSlots { get; set; }
    public int RemainingOverflowSlots { get; set; }
    public bool IsOverflowing { get; set; }
    public IReadOnlyList<TripGroupBookingCardResponse> Bookings { get; set; } = [];
}
