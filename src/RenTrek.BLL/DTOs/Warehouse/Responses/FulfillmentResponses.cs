namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class HandoverChecklistResponse
{
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public int TotalRequiredItems { get; set; }
    public int TotalScannedItems { get; set; }
    public bool IsReadyToComplete { get; set; }
    public List<HandoverChecklineItemResponse> Items { get; set; } = [];
}

public class HandoverChecklineItemResponse
{
    public Guid RentalOrderItemId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }
    public int ScannedQuantity { get; set; }
    public List<HandoverScannedAssetResponse> ScannedAssets { get; set; } = [];
}

public class HandoverScannedAssetResponse
{
    public Guid AssignmentId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScannedAt { get; set; }
}

public class HandoverScanResultResponse
{
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public HandoverProgressResponse Progress { get; set; } = new();
}

public class HandoverProgressResponse
{
    public int ScannedItems { get; set; }
    public int TotalRequiredItems { get; set; }
    public bool IsCompleted { get; set; }
}

public class ReturnChecklistResponse
{
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public bool IsOverdue { get; set; }
    public int OverdueDays { get; set; }
    public int TotalIssuedItems { get; set; }
    public int TotalReturnedItems { get; set; }
    public int TotalLostItems { get; set; }
    public bool IsAllReturned { get; set; }
    public List<ReturnChecklistItemResponse> Items { get; set; } = [];
}

public class ReturnChecklistItemResponse
{
    public Guid AssignmentId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; }
}

public class ReturnScanResultResponse
{
    public string AssetCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ReturnCondition { get; set; } = string.Empty;
    public string? ConditionNote { get; set; }
    public bool IsOverdue { get; set; }
    public int OverdueDays { get; set; }
    public decimal? SuggestedLateFee { get; set; }
    public Guid? CreatedDamageChargeId { get; set; }
    public string Status { get; set; } = "Returned";
}

public class RentalChargeDetailResponse
{
    public Guid ChargeId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string ChargeType { get; set; } = string.Empty;
    public string? EquipmentAssetCode { get; set; }
    public string? ProductName { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
}
