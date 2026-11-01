namespace RenTrek.DAL.Entities;

public sealed class PaymentWebhookReceipt
{
    public Guid PaymentWebhookReceiptId { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "SePay";
    public string ProviderEventId { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string ProtectedPayload { get; set; } = "";
    public string Status { get; set; } = "Received";
    public string? FailureReason { get; set; }
    public bool HasConflictingPayload { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}
