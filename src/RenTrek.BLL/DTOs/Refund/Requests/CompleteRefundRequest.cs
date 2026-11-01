namespace RenTrek.BLL.DTOs.Refund.Requests;

public sealed class CompleteRefundRequest
{
    public string TransferReference { get; set; } = "";
    public DateTimeOffset TransferredAt { get; set; }
    public string? EvidenceReference { get; set; }
}
