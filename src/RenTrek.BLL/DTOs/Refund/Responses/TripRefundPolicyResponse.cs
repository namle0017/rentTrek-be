namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Chính sách huỷ / hoàn tiền của một chuyến đi, công khai cho khách xem trước khi đặt.
/// </summary>
public sealed class TripRefundPolicyResponse
{
    public Guid RefundPolicyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<RuleItem> Rules { get; set; } = [];
    public RescheduleTerms? Reschedule { get; set; }
    public int NoShowGraceMinutes { get; set; }

    public sealed class RuleItem
    {
        public int MinHoursBefore { get; set; }
        public decimal RefundPercentage { get; set; }
    }

    public sealed class RescheduleTerms
    {
        public int MinHoursBefore { get; set; }
        public decimal Fee { get; set; }
        public int MaxTimes { get; set; }
    }
}
