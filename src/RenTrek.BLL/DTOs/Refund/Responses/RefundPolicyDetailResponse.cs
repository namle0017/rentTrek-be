namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Chi tiết chính sách hoàn tiền kèm bảng bậc, lịch sử phiên bản và các cờ thao tác (20.3).
/// </summary>
public sealed class RefundPolicyDetailResponse : RefundPolicyListItemResponse
{
    public DateTime UpdatedAt { get; set; }

    /// <summary>true khi đã có chuyến đi hoặc đơn hàng dùng chính sách: không cho sửa / xoá trực tiếp.</summary>
    public bool IsLocked { get; set; }

    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanArchive { get; set; }
    public bool CanActivate { get; set; }
    public bool CanClone { get; set; }

    public int? RescheduleMinHoursBefore { get; set; }
    public decimal RescheduleFee { get; set; }
    public int MaxReschedules { get; set; }
    public int NoShowGraceMinutes { get; set; }

    public IReadOnlyList<RefundPolicyRuleResponse> Rules { get; set; } = [];

    public IReadOnlyList<RefundPolicyVersionResponse> Versions { get; set; } = [];
}
