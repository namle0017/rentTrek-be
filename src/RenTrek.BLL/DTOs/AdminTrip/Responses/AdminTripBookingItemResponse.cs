namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin một đơn đặt chỗ kèm danh sách hành khách của chuyến đi tại Tab Booking O11 (8.11).
/// </summary>
public sealed class AdminTripBookingItemResponse
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid CustomerUserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public int ParticipantCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string GroupingPreference { get; set; } = string.Empty;
    public bool IsSplit { get; set; }
    public string? SplitReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<AdminTripParticipantItemResponse> Participants { get; set; } = [];
}
