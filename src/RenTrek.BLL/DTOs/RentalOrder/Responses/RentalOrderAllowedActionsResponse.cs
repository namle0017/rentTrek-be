namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Các thao tác khách được phép làm với đơn thuê (do server quyết định).
/// </summary>
public class RentalOrderAllowedActionsResponse
{
    public bool CanPay { get; set; }

    public bool CanCancel { get; set; }

    public string? CancelBlockedReason { get; set; }
    /// <summary>Đơn đã thanh toán, chưa nhận đồ: được huỷ theo chính sách (xem cancellation-preview).</summary>
    public bool CanCancelPaid { get; set; }
    /// <summary>Đã giao đồ, khách chưa xác nhận đã nhận và kiểm tra.</summary>
    public bool CanConfirmReceipt { get; set; }
    /// <summary>Có hồ sơ hoàn tiền đang chờ khách nhập STK.</summary>
    public bool CanSubmitBankInfo { get; set; }
    /// <summary>Có khoản thu sau (gia hạn / phí vượt cọc) đang chờ thanh toán.</summary>
    public bool CanPayAdditional { get; set; }
    public bool CanExtend { get; set; }
}
