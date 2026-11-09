namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>
/// Thông tin chỉ đọc của link xem chuyến: tour, danh sách họ tên và lịch hoạt động.
/// </summary>
public sealed class InvitationPreviewResponse
{
    public Guid OrderId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset TripStartAt { get; set; }
    public DateTimeOffset TripEndAt { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public IReadOnlyList<string> ParticipantNames { get; set; } = [];
    public IReadOnlyList<TripActivityPreviewResponse> Activities { get; set; } = [];
}
