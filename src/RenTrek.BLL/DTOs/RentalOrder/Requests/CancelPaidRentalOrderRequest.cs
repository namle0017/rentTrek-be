using RenTrek.BLL.DTOs.Refund.Requests;

namespace RenTrek.BLL.DTOs.RentalOrder.Requests;

/// <summary>Khách huỷ đơn thuê đã thanh toán, chưa nhận đồ (theo chính sách cọc / huỷ).</summary>
public sealed class CancelPaidRentalOrderRequest
{
    public string? Reason { get; set; }

    /// <summary>Số tiền hoàn khách đã thấy ở cancellation-preview; lệch thì 409 REFUND_AMOUNT_CHANGED.</summary>
    public decimal ExpectedRefundAmount { get; set; }

    /// <summary>Bắt buộc khi có tiền hoàn.</summary>
    public RefundBankAccountRequest? BankAccount { get; set; }

    public bool ConsentAccepted { get; set; }

    public string? ConsentVersion { get; set; }
}
