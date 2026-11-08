namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Chi tiết chính sách hoàn tiền kèm bảng bậc (20.3).
/// </summary>
public sealed class RefundPolicyDetailResponse
{
    public Guid PolicyId { get; set; }
    public Guid PolicyFamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>true khi đã có chuyến đi hoặc đơn hàng dùng chính sách: không cho sửa trực tiếp.</summary>
    public bool IsLocked { get; set; }

    public IReadOnlyList<RefundPolicyRuleResponse> Rules { get; set; } = [];
}
