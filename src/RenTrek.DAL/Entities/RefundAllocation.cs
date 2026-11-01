namespace RenTrek.DAL.Entities;

public sealed class RefundAllocation
{
    public Guid RefundAllocationId { get; set; } = Guid.NewGuid();
    public Guid RefundRequestId { get; set; }
    public Guid BankTransactionId { get; set; }
    public decimal Amount { get; set; }
}
