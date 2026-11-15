namespace RenTrek.BLL.DTOs.RentalPolicy.Requests;

/// <summary>Tạo version mới của chính sách thuê đồ (kích hoạt ngay, archive bản đang Active).</summary>
public class CreateRentalPolicyVersionRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Version dùng làm mẫu khi tạo (chỉ ghi vào audit log, không ảnh hưởng dữ liệu).</summary>
    public Guid? SourcePolicyId { get; set; }

    public decimal DepositPercent { get; set; }
    public int FreeCancelLimit { get; set; }
    public int FreeCancelWindowDays { get; set; }
    public decimal OverLimitCancelFeePercent { get; set; }
    public int ReturnGraceHours { get; set; }
    public int ExtensionMaxDaysLate { get; set; }
    public int SuspectedLossAfterDays { get; set; }
    public int ReceiptAutoConfirmHours { get; set; }
    public int MinPhotosPerAsset { get; set; }
    public List<RentalPolicyTierRequest> Tiers { get; set; } = [];
}
