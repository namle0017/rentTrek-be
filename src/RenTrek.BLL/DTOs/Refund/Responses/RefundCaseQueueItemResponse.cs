namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Một dòng trong hàng đợi Admin; không có số tài khoản đầy đủ.
/// </summary>
public sealed class RefundCaseQueueItemResponse
{
    public Guid RefundCaseId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;

    /// <summary>TripCancellation | RentalCancellation | RentalNoShow | RentalDepositReturn.</summary>
    public string Source { get; set; } = string.Empty;

    public string? TourName { get; set; }
    public DateTimeOffset? TripStartAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal OrderTotalAmount { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? BankCode { get; set; }
    public string MaskedAccount { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public int? MinHoursBefore { get; set; }
    public decimal? RefundPercentage { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>Lý do khiếu nại khách nhập khi gửi lại hồ sơ bị từ chối.</summary>
    public string? AppealReason { get; set; }

    public string? RejectionReason { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Thời điểm xuất Excel lần đầu cho kế toán; null = đã duyệt, chờ xuất.</summary>
    public DateTimeOffset? ExportedAt { get; set; }
    public string? ProviderTransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int RowVersion { get; set; }

    /// <summary>RejectionReason khi Status = Failed, ngược lại null.</summary>
    public string? FailureReason { get; set; }

    public IReadOnlyList<RefundProofResponse> Proofs { get; set; } = [];
}
