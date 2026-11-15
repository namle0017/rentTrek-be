namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Body tạo / sửa chính sách hoàn tiền (20.2, 20.4). Rules được lưu cùng chính sách trong một transaction.
/// </summary>
public sealed class SaveRefundPolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public List<RefundPolicyRuleRequest> Rules { get; set; } = [];

    /// <summary>Chỉ dùng khi tạo: true lưu nháp, false kích hoạt ngay. Bỏ qua khi sửa.</summary>
    public bool SaveAsDraft { get; set; }
}
