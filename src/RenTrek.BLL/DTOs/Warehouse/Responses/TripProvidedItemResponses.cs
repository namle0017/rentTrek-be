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

public class TripAssignedAssetResponse
{
    public Guid AssignmentId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; }
}

public class AvailableAssetResponse
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? ConditionNote { get; set; }
    public string AssetStatus { get; set; } = string.Empty;
}
