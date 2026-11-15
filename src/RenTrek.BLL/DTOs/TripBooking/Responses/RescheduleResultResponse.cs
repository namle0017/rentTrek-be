using RenTrek.BLL.DTOs.Refund.Responses;

namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>Kết quả đổi lịch: Completed (đã đổi) hoặc PendingPayment (chờ trả chênh lệch).</summary>
public sealed class RescheduleResultResponse
{
    public Guid RescheduleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid ToTripId { get; set; }
    public decimal PriceDiff { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public RefundCaseSummaryResponse? RefundCase { get; set; }
}
