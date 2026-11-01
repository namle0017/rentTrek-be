namespace RenTrek.DAL.Entities;

public sealed class BankTransaction
{
    public Guid BankTransactionId { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "SePay";
    public string ProviderTransactionId { get; set; } = "";
    public Guid ReceiptId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? OrderId { get; set; }
    public decimal Amount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public string Status { get; set; } = "Unmatched";
    public string? ReconciliationReason { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
}
