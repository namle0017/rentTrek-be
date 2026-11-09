namespace RenTrek.DAL.Models;

/// <summary>
/// Ngữ cảnh sở hữu và điều kiện đủ quyền đánh giá của một lượt tham gia chuyến đi.
/// </summary>
public sealed class ParticipantReviewContextRow
{
    public Guid ParticipantId { get; set; }
    public string ParticipantStatus { get; set; } = string.Empty;
    public string TripStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string OrderPaymentStatus { get; set; } = string.Empty;
    public bool HasReview { get; set; }
}
