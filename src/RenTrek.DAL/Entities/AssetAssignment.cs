namespace RenTrek.DAL.Entities;

public class AssetAssignment
{
    public Guid AssetAssignmentId { get; set; }
    public Guid? RentalOrderItemId { get; set; }
    public Guid EquipmentAssetId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; }
    public string Status { get; set; } = null!;
    public Guid? TripProvidedItemId { get; set; }
    public Guid? AppliedPromotionId { get; set; }
    public DateTime ReservedFrom { get; set; }
    public DateTime ReservedUntil { get; set; }
    public DateTime CreatedAt { get; set; }

    public AppliedPromotion? AppliedPromotion { get; set; }
    public EquipmentAsset EquipmentAsset { get; set; } = null!;
    public RentalOrderItem? RentalOrderItem { get; set; }
    public TripProvidedItem? TripProvidedItem { get; set; }
}
