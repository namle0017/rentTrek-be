namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>Báo giá huỷ đơn đã thanh toán.</summary>
public class RentalCancellationPreviewResponse
{
    public Guid OrderId { get; set; }
    public bool Allowed { get; set; }
    public string? BlockedReason { get; set; }
    /// <summary>FreeCancel | OverLimitCancel | LateCancel | NoShow.</summary>
    public string CancellationType { get; set; } = string.Empty;
    public int DaysLate { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal Fee { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal? AppliedTierPercent { get; set; }
    public int FreeCancellationsUsed { get; set; }
    public int FreeCancelLimit { get; set; }
    public int FreeCancelWindowDays { get; set; }
    public bool RequiresBankAccount { get; set; }
    public string ConsentVersion { get; set; } = string.Empty;
}
