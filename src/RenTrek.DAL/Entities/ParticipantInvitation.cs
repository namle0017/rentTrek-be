namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể participant invitation trong mô hình dữ liệu RenTrek.
/// </summary>
public sealed class ParticipantInvitation
{
    public Guid ParticipantInvitationId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid BookingOrderId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public Guid? RedeemedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public BookingParticipant Participant { get; set; } = null!;
    public TripBookingOrder BookingOrder { get; set; } = null!;
}
