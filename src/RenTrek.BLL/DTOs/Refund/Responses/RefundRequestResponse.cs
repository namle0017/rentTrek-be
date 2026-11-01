namespace RenTrek.BLL.DTOs.Refund.Responses;

public sealed class RefundRequestResponse
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Replayed { get; set; }
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string? Note { get; set; }
    public string MaskedAccount { get; set; } = "";
    public RefundBankDetailsResponse? BankDetails { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public string? RejectReason { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? TransferReference { get; set; }
    public DateTimeOffset? TransferredAt { get; set; }
    public string? EvidenceReference { get; set; }
    public int PolicyVersion { get; set; }
    public decimal RefundPercentage { get; set; }
}

public sealed record RefundBankDetailsResponse(string BankCode, string AccountNumber, string AccountHolderName);
public sealed record RefundEligibilityResponse(bool Eligible, string? Reason, decimal Amount, int PolicyVersion, decimal RefundPercentage);
