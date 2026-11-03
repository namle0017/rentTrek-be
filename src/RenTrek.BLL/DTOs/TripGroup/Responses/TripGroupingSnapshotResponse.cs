namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Snapshot điều phối chia đoàn của một Trip theo phạm vi quyền (Staff/Admin hoặc Guide).
/// </summary>
public sealed class TripGroupingSnapshotResponse
{
    public Guid TripId { get; set; }
    public string TripStatus { get; set; } = string.Empty;
    public string GroupingStatus { get; set; } = string.Empty;
    public int TotalSlots { get; set; }
    public int GroupMaxParticipants { get; set; }
    public int OverflowSlots { get; set; }
    public int StandardCapacity { get; set; }
    public int OperationalCapacity { get; set; }
    public int AssignedParticipants { get; set; }
    public int UnassignedParticipants { get; set; }
    public int RowVersion { get; set; }
    public bool CanPublish { get; set; }
    public IReadOnlyList<PublishBlockerResponse> PublishBlockers { get; set; } = [];
    public IReadOnlyList<TripGroupCardResponse> Groups { get; set; } = [];
    public IReadOnlyList<TripGroupBookingCardResponse> UnassignedBookings { get; set; } = [];
    public bool ProvidedItemsNeedsRecalc { get; set; }
    public IReadOnlyList<TripProvidedItemSummaryResponse> ProvidedItems { get; set; } = [];
}
