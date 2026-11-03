namespace RenTrek.DAL.Models;

/// <summary>
/// Phân bổ tài sản vật lý cho một dòng thuê (kèm mã tài sản và tên thiết bị).
/// </summary>
public class RentalOrderAssignmentRow
{
    public Guid AssignmentId { get; set; }
    public Guid? RentalOrderItemId { get; set; }
    public Guid EquipmentAssetId { get; set; }

    /// <summary>Tình trạng thiết bị hiện tại (equipment_assets.asset_status).</summary>
    public string AssetStatus { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; }
    public DateTime ReservedFrom { get; set; }
    public DateTime ReservedUntil { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
}
