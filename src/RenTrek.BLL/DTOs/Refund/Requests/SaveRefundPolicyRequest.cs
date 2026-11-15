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

    /// <summary>Chỉ dùng cho loại Tour. null = không cho đổi lịch.</summary>
    public int? RescheduleMinHoursBefore { get; set; }
    public decimal RescheduleFee { get; set; }
    public int MaxReschedules { get; set; }
    public int NoShowGraceMinutes { get; set; } = 30;

    /// <summary>Chỉ dùng khi tạo: true lưu nháp, false kích hoạt ngay. Bỏ qua khi sửa.</summary>
    public bool SaveAsDraft { get; set; }
}
