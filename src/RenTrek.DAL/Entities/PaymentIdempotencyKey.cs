namespace RenTrek.DAL.Entities;

public sealed class PaymentIdempotencyKey
{
    public Guid PaymentIdempotencyKeyId { get; set; } = Guid.NewGuid();
    public Guid CustomerUserId { get; set; }
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid PaymentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
