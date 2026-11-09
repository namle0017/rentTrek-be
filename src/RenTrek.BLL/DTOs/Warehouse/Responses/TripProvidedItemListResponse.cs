namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class TripProvidedItemListResponse
{
    public Guid TripId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalParticipants { get; set; }
    public int TotalGroups { get; set; }
    public bool HasShortage { get; set; }
    public List<TripProvidedItemDetailResponse> Items { get; set; } = [];
}
