namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class TripProvidedItemDetailResponse
{
    public Guid TripProvidedItemId { get; set; }
    public Guid TourProvidedItemId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string QuantityBasis { get; set; } = "PerPerson";
    public int TourConfigQuantity { get; set; }
    public int RequiredQuantity { get; set; }
    public int AssignedCount { get; set; }
    public int MissingCount { get; set; }
    public Guid? PreparedByUserId { get; set; }
    public string? PreparedByUserName { get; set; }
    public string Status { get; set; } = "Missing";
    public List<TripAssignedAssetResponse> AssignedAssets { get; set; } = [];
}
