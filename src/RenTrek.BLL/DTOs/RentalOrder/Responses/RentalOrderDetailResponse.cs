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

    public List<RentalOrderPaymentResponse> Payments { get; set; } = [];

    public List<RentalOrderTimelineEventResponse> Timeline { get; set; } = [];

    public RentalOrderAllowedActionsResponse AllowedActions { get; set; } = new();
}
