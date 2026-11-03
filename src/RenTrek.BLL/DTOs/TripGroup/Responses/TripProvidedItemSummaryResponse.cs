namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Dòng nhu cầu đồ cấp chuyến theo FR-I01 (PER_GROUP / PER_PARTICIPANT).
/// </summary>
public sealed class TripProvidedItemSummaryResponse
{
    public Guid TripProvidedItemId { get; set; }
    public Guid TourProvidedItemId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string QuantityBasis { get; set; } = string.Empty;
    public int UnitQuantity { get; set; }
    public int RequiredQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
}
