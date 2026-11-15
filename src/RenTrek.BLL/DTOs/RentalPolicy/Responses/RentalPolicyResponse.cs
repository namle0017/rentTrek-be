namespace RenTrek.BLL.DTOs.RentalPolicy.Responses;

/// <summary>Chính sách thuê đồ (cọc, huỷ, trễ nhận / trả).</summary>
public class RentalPolicyResponse
{
    public Guid? RentalPolicyId { get; set; }
    public Guid? PolicyFamilyId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal DepositPercent { get; set; }
    public int FreeCancelLimit { get; set; }
    public int FreeCancelWindowDays { get; set; }
    public decimal OverLimitCancelFeePercent { get; set; }
    public int ReturnGraceHours { get; set; }
    public int ExtensionMaxDaysLate { get; set; }
    public int SuspectedLossAfterDays { get; set; }
    public int ReceiptAutoConfirmHours { get; set; }
    public int MinPhotosPerAsset { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public List<RentalPolicyTierResponse> Tiers { get; set; } = [];
}
