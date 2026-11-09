namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Body tạo / sửa chính sách hoàn tiền (20.2, 20.4).
/// </summary>
public sealed class SaveRefundPolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
}
