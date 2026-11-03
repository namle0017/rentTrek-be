namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Một dòng trong hàng đợi Admin; không có số tài khoản đầy đủ.
/// </summary>
public sealed class RefundCaseQueueItemResponse
{
    public Guid RefundCaseId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? TourName { get; set; }
    public DateTimeOffset? TripStartAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal OrderTotalAmount { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string MaskedAccount { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public int? MinHoursBefore { get; set; }
    public decimal? RefundPercentage { get; set; }
    public string? CancelReason { get; set; }
    public string? ProviderTransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int RowVersion { get; set; }
}
