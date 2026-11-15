namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể trip booking order trong mô hình dữ liệu RenTrek.
/// </summary>
public sealed class TripBookingOrder
{
    public Guid OrderId { get; set; }
    public Guid TripId { get; set; }
    public int ParticipantCount { get; set; }
    public decimal UnitPrice { get; set; }
    public string GroupingPreference { get; set; } = string.Empty;
    public bool IsSplit { get; set; }
    public string? SplitReason { get; set; }
    public Guid? SplitApprovedByUserId { get; set; }
    public DateTimeOffset? SplitAt { get; set; }
    public int InviteVersion { get; set; }
    public int RescheduleCount { get; set; }

    public Order Order { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
    public ICollection<BookingParticipant> Participants { get; set; } = [];
}
