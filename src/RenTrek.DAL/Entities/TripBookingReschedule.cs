namespace RenTrek.DAL.Entities;

/// <summary>Một lần đổi lịch booking sang chuyến khác cùng tour (chờ trả chênh lệch hoặc đã hoàn tất).</summary>
public sealed class TripBookingReschedule
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid FromTripId { get; set; }
    public Guid ToTripId { get; set; }
    public int ParticipantCount { get; set; }
    public decimal PriceDiff { get; set; }
    public decimal Fee { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
