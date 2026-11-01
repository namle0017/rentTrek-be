namespace RenTrek.BLL.DTOs.Payment.Responses;

public sealed class ReconciliationItemResponse
{
    public Guid ReceiptId { get; set; }
    public Guid? BankTransactionId { get; set; }
    public Guid? OrderId { get; set; }
    public decimal? Amount { get; set; }
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public bool HasConflict { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
