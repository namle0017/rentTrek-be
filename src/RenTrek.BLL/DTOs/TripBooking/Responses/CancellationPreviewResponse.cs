namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>
/// Xem trước số tiền hoàn trước khi khách xác nhận hủy booking.
/// </summary>
public sealed class CancellationPreviewResponse
{
    public bool CanCancel { get; set; }
    public string? BlockedReason { get; set; }
    public PolicyInfo? RefundPolicy { get; set; }
    public string AppliedCase { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public decimal EstimatedRefundAmount { get; set; }
    public string ProcessingMethod { get; set; } = "ManualBankTransfer";
    public string ConsentVersion { get; set; } = string.Empty;

    public sealed class PolicyInfo
    {
        public Guid RefundPolicyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Version { get; set; }
        public IReadOnlyList<RuleInfo> Rules { get; set; } = [];
    }

    public sealed class RuleInfo
    {
        public int MinHoursBefore { get; set; }
        public decimal RefundPercentage { get; set; }
    }
}
