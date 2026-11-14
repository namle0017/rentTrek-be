namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Chi tiết đơn thuê của khách. Hệ thống không thu cọc nên DepositAmount luôn bằng 0.
/// </summary>
public class RentalOrderDetailResponse
{
    public Guid Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string OrderType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? HoldExpiresAt { get; set; }

    public DateTime RentalStartAt { get; set; }

    public DateTime RentalEndAt { get; set; }

    public int BillableDays { get; set; }

    public string? Note { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public RentalOrderHandledByResponse? HandledBy { get; set; }

    public RentalOrderRefundPolicyResponse RefundPolicy { get; set; } = new();

    public List<RentalOrderLineResponse> Lines { get; set; } = [];

    public List<RentalOrderAssetResponse> Assets { get; set; } = [];

    public RentalOrderAmountsResponse Amounts { get; set; } = new();

    public List<RentalOrderChargeResponse> Charges { get; set; } = [];
    public RentalOrderDepositSettlementResponse DepositSettlement { get; set; } = new();

    public List<RentalOrderPaymentResponse> Payments { get; set; } = [];

    public List<RentalOrderTimelineEventResponse> Timeline { get; set; } = [];

    public RentalOrderAllowedActionsResponse AllowedActions { get; set; } = new();

    /// <summary>Chính sách cọc / phạt đã snapshot lúc đặt.</summary>
    public RenTrek.BLL.DTOs.RentalPolicy.Responses.RentalPolicyResponse? RentalPolicy { get; set; }
    public string? CancellationType { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? CustomerReceivedConfirmedAt { get; set; }
    /// <summary>Customer | Auto.</summary>
    public string? ReceiptConfirmSource { get; set; }
    /// <summary>Hồ sơ hoàn tiền mới nhất của đơn (nếu có).</summary>
    public RentalOrderRefundCaseResponse? RefundCase { get; set; }
    /// <summary>Kết quả quyết toán cọc đã chốt (sau khi admin duyệt).</summary>
    public RentalOrderSettlementResponse? Settlement { get; set; }
    /// <summary>Khoản thu sau đang chờ thanh toán (gia hạn / phí vượt cọc).</summary>
    public RentalOrderPaymentResponse? PendingAdditionalPayment { get; set; }
    /// <summary>Ảnh bằng chứng giao / nhận lại.</summary>
    public List<RentalEvidencePhotoResponse> EvidencePhotos { get; set; } = [];
}
