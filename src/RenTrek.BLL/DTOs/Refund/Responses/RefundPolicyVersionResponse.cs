namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Một phiên bản trong lịch sử của họ chính sách.
/// </summary>
public sealed class RefundPolicyVersionResponse
{
    public Guid PolicyId { get; set; }
    public int Version { get; set; }
    public string DisplayStatus { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public int UsageCount { get; set; }
}
