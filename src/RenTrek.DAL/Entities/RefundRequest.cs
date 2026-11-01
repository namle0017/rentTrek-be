namespace RenTrek.DAL.Entities;

public sealed class RefundRequest
{
    public Guid RefundRequestId { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid CustomerUserId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ProtectedBankDetails { get; set; } = "";
    public string MaskedAccount { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string? Note { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "PendingReview";
    public Guid RefundPolicyId { get; set; }
    public Guid RefundPolicyRuleId { get; set; }
    public string PolicySnapshot { get; set; } = "";
    public DateTimeOffset SubmittedAt { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
    public Guid? CompletedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? TransferReference { get; set; }
    public DateTimeOffset? TransferredAt { get; set; }
    public string? EvidenceReference { get; set; }
    public string? CompletionKey { get; set; }
    public string? CompletionHash { get; set; }
    public Guid? RefundPaymentId { get; set; }
}
