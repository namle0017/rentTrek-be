namespace RenTrek.DAL.Entities;

/// <summary>
/// Chính sách thuê đồ có version: % cọc, giới hạn huỷ miễn phí, ân hạn trả, mốc nghi mất... Đơn snapshot policy lúc đặt.
/// </summary>
public class RentalPolicy
{
    public Guid RentalPolicyId { get; set; }
    public Guid PolicyFamilyId { get; set; }
    public int Version { get; set; } = 1;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal DepositPercent { get; set; }
    public int FreeCancelLimit { get; set; }
    public int FreeCancelWindowDays { get; set; }
    public decimal OverLimitCancelFeePercent { get; set; }
    public int ReturnGraceHours { get; set; }
    public int ExtensionMaxDaysLate { get; set; }
    public int SuspectedLossAfterDays { get; set; }
    public int ReceiptAutoConfirmHours { get; set; }
    public int MinPhotosPerAsset { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<RentalPolicyTier> Tiers { get; set; } = new List<RentalPolicyTier>();
}
