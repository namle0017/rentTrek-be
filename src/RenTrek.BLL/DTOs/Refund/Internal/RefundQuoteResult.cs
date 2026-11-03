using RenTrek.DAL.Entities;

namespace RenTrek.BLL.DTOs.Refund.Internal;

/// <summary>
/// Kết quả tính hoàn theo đúng phiên bản chính sách đã chốt trên Order.
/// </summary>
public sealed class RefundQuoteResult
{
    public decimal PaidAmount { get; init; }
    public decimal RefundAmount { get; init; }
    public string AppliedCase { get; init; } = string.Empty;
    public RefundPolicy? Policy { get; init; }
    public RefundPolicyRule? Rule { get; init; }
}
