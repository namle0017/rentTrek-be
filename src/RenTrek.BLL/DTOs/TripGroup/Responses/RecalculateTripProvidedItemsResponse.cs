namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Kết quả tính lại nhu cầu đồ cấp chuyến cho Trip theo số group có người và số người tham gia hiệu lực.
/// </summary>
public sealed class RecalculateTripProvidedItemsResponse
{
    public Guid TripId { get; set; }
    public int RowVersion { get; set; }
    public int ActiveGroupCount { get; set; }
    public int ActiveParticipantCount { get; set; }
    public bool ProvidedItemsNeedsRecalc { get; set; }
    public IReadOnlyList<TripProvidedItemSummaryResponse> ProvidedItems { get; set; } = [];
}
