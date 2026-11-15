namespace RenTrek.DAL.Entities;

public class RentalCharge
{
    public Guid RentalChargeId { get; set; }
    public Guid RentalOrderId { get; set; }
    public Guid? EquipmentAssetId { get; set; }
    public string ChargeType { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid? CreatedByUserId { get; set; }
    public int? TierMinDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }

    public Account? CreatedByUser { get; set; }
    public EquipmentAsset? EquipmentAsset { get; set; }
    public RentalOrder RentalOrder { get; set; } = null!;
}
