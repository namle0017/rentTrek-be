namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng chính sách hoàn tiền cho admin, kèm số bậc, số chuyến, số đơn và số phiên bản trong họ.
/// </summary>
public class AdminRefundPolicyRow
{
    public Guid RefundPolicyId { get; set; }
    public Guid PolicyFamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PolicyType { get; set; } = string.Empty;
    public int Version { get; set; }
    public int VersionsCount { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }

    /// <summary>Trạng thái lưu: Draft | Active | Archived.</summary>
    public string Status { get; set; } = string.Empty;

    public int RulesCount { get; set; }
    public int TripsCount { get; set; }
    public int OrdersCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Số chuyến đi cộng số đơn hàng đang liên kết với chính sách.</summary>
    public int UsageCount => TripsCount + OrdersCount;
}
