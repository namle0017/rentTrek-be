namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Một dòng danh sách chính sách hoàn tiền (20.1).
/// </summary>
public sealed class RefundPolicyListItemResponse
{
    public Guid PolicyId { get; set; }
    public Guid PolicyFamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Số chuyến đi cộng số đơn hàng đang liên kết với chính sách.</summary>
    public int ActiveTripsCount { get; set; }
}
