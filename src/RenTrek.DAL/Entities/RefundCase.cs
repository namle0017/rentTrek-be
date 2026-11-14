namespace RenTrek.DAL.Entities;

/// <summary>
/// Hồ sơ hoàn tiền thủ công: điều phối quy trình, còn dòng tiền vẫn nằm ở <see cref="Payment"/>.
/// </summary>
public sealed class RefundCase
{
    public Guid RefundCaseId { get; set; }
    public Guid OrderId { get; set; }
    public Guid RefundPaymentId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid? ApprovedByAdminUserId { get; set; }
    public Guid? RefundPolicyRuleId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = "TripCancellation";
    public string? BankCode { get; set; }
    public string? BankAccountLast4 { get; set; }
    public byte[]? PayoutDetailsCiphertext { get; set; }
    public byte[]? PayoutDetailsNonce { get; set; }
    public byte[]? PayoutDetailsTag { get; set; }
    public string? ConsentVersion { get; set; }
    public DateTimeOffset? ConsentAcceptedAt { get; set; }
    public DateTimeOffset? BankInfoRequestedAt { get; set; }
    public DateTimeOffset? BankInfoSubmittedAt { get; set; }
    public string? ProviderTransactionId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? SensitiveDataPurgedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Order Order { get; set; } = null!;
    public Payment RefundPayment { get; set; } = null!;
    public Account CustomerUser { get; set; } = null!;
    public RefundPolicyRule? RefundPolicyRule { get; set; }
}
