namespace RenTrek.BLL.DTOs.TripBooking;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ trip booking.
/// </summary>
public sealed class TripBookingResponse
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid TripId { get; set; }
    public DateTimeOffset TripStartAt { get; set; }
    public DateTimeOffset TripEndAt { get; set; }
    public string TripStatus { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public bool CanManage { get; set; }
    public IReadOnlyList<BookingParticipantResponse> Participants { get; set; } = [];
}
