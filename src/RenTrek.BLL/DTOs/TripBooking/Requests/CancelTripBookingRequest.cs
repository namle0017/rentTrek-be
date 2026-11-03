using RenTrek.BLL.DTOs.Refund.Requests;

namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Yêu cầu hủy toàn bộ booking chuyến đi.
/// </summary>
public sealed class CancelTripBookingRequest
{
    public string? Reason { get; set; }

    /// <summary>Số tiền khách đã thấy ở preview; lệch số BE tính lại thì trả 409 REFUND_AMOUNT_CHANGED.</summary>
    public decimal? ExpectedRefundAmount { get; set; }
    public RefundBankAccountRequest? BankAccount { get; set; }
    public string? ConsentVersion { get; set; }
    public bool ConsentAccepted { get; set; }
}
