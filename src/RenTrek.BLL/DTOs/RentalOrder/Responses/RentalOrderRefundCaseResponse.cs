namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>Tóm tắt hồ sơ hoàn tiền của đơn thuê (STK luôn che, chỉ 4 số cuối).</summary>
public class RentalOrderRefundCaseResponse
{
    public Guid RefundCaseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public string? BankAccountLast4 { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Chứng từ chuyển khoản (chỉ khi hồ sơ Completed); tải qua GET /refund-cases/{id}/proofs/{proofId}.</summary>
    public IReadOnlyList<RenTrek.BLL.DTOs.Refund.Responses.RefundProofResponse> Proofs { get; set; } = [];
}
