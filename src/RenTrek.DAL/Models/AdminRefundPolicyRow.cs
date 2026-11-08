namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng danh sách chính sách hoàn tiền cho admin, kèm số chuyến và đơn đang tham chiếu.
/// </summary>
public class AdminRefundPolicyRow
{
    public Guid RefundPolicyId { get; set; }
    public Guid PolicyFamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Số chuyến đi cộng số đơn hàng đang liên kết với chính sách.</summary>
    public int UsageCount { get; set; }
}
