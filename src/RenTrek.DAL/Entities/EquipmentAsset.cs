namespace RenTrek.DAL.Entities;

public class EquipmentAsset
{
    public Guid EquipmentAssetId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetStatus { get; set; } = null!;
    public string? ConditionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();
    public ProductVariant ProductVariant { get; set; } = null!;
    public ICollection<RentalCharge> RentalCharges { get; set; } = new List<RentalCharge>();
}
