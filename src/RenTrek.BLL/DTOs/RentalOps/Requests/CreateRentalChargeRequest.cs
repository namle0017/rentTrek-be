using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.RentalOps.Requests;

/// <summary>Staff lập phí phát sinh (trạng thái Assessed ngay).</summary>
public sealed class CreateRentalChargeRequest
{
    /// <summary>LateReturn | Damage | Loss | Other</summary>
    public string ChargeType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Thiết bị chịu phí; bắt buộc với Damage / Loss.</summary>
    public Guid? AssignmentId { get; set; }
}
