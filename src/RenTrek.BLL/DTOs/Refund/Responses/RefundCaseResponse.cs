namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Tiến độ hoàn tiền khách được xem; tài khoản luôn ở dạng đã che.
/// </summary>
public sealed class RefundCaseResponse
{
    public Guid RefundCaseId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string MaskedAccount { get; set; } = string.Empty;
    public IReadOnlyList<RefundTimelineEventResponse> Timeline { get; set; } = [];
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Mã giao dịch ngân hàng của lần chuyển khoản hoàn tiền; chỉ có khi hồ sơ Completed.</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>Chứng từ chuyển khoản; chỉ có khi hồ sơ Completed.</summary>
    public IReadOnlyList<RefundProofResponse> Proofs { get; set; } = [];

    /// <summary>Lý do chuyển khoản thất bại khi Status = Failed, hoặc lý do admin trả hồ sơ về chờ STK.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Lý do admin từ chối khi Status = Rejected.</summary>
    public string? RejectionReason { get; set; }

    public string OrderType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? TourName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Hồ sơ đang chờ khách nhập STK.</summary>
    public bool CanSubmitBankInfo { get; set; }

    /// <summary>Hồ sơ bị từ chối / thất bại: khách nhập STK và gửi lại yêu cầu.</summary>
    public bool CanResubmit { get; set; }
}
