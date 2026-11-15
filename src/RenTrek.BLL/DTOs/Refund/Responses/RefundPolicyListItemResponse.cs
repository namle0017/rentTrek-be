namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Một dòng danh sách chính sách hoàn tiền (20.1).
/// </summary>
public class RefundPolicyListItemResponse
{
    public Guid PolicyId { get; set; }
    public Guid PolicyFamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PolicyType { get; set; } = string.Empty;
    public int Version { get; set; }
    public int VersionsCount { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }

    /// <summary>Trạng thái lưu: Draft | Active | Archived.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Trạng thái hiển thị: Draft | Scheduled | Active | Expired | Archived.</summary>
    public string DisplayStatus { get; set; } = string.Empty;

    public int RulesCount { get; set; }
    public int TripsCount { get; set; }
    public int OrdersCount { get; set; }

    /// <summary>Số chuyến đi cộng số đơn hàng đang liên kết với chính sách.</summary>
    public int UsageCount { get; set; }

    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
