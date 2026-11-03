namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class AssetDetailResponse : AssetResponse
{
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<AssetAssignmentHistoryResponse> RecentAssignments { get; set; } = [];
    public IReadOnlyList<AssetRentalChargeResponse> RecentCharges { get; set; } = [];
}
