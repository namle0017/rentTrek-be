namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Tiến độ hoàn tiền khách được xem; tài khoản luôn ở dạng đã che.
/// </summary>
public sealed class RefundCaseResponse
{
    public Guid RefundCaseId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string MaskedAccount { get; set; } = string.Empty;
    public IReadOnlyList<RefundTimelineEventResponse> Timeline { get; set; } = [];
    public DateTimeOffset? CompletedAt { get; set; }
}
