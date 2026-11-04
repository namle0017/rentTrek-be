using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class HandoverScanRequest
{
    [Required(ErrorMessage = "Mã thiết bị không được để trống.")]
    public string AssetCode { get; set; } = string.Empty;
}

public class CompleteHandoverRequest
{
    public string? Note { get; set; }
}

public class ReturnScanRequest
{
    [Required(ErrorMessage = "Mã thiết bị không được để trống.")]
    public string AssetCode { get; set; } = string.Empty;

    public string ReturnCondition { get; set; } = "Normal";

    public string? ConditionNote { get; set; }

    public bool AutoCreateDamageCharge { get; set; }

    public decimal? SuggestedDamageAmount { get; set; }
}

public class ReportLostAssetRequest
{
    [Required(ErrorMessage = "Mã thiết bị không được để trống.")]
    public string AssetCode { get; set; } = string.Empty;

    public string? Note { get; set; }

    public decimal? ChargeAmount { get; set; }
}

public class CreateRentalChargeRequest
{
    [Required(ErrorMessage = "Loại phí không được để trống.")]
    public string ChargeType { get; set; } = "Damage";

    [Range(0, 100000000, ErrorMessage = "Số tiền phí phải từ 0 trở lên.")]
    public decimal Amount { get; set; }

    public Guid? EquipmentAssetId { get; set; }

    [Required(ErrorMessage = "Lý do thu phí không được để trống.")]
    public string Reason { get; set; } = string.Empty;
}

public class VoidRentalChargeRequest
{
    [Required(ErrorMessage = "Lý do hủy phí không được để trống.")]
    public string Reason { get; set; } = string.Empty;
}
