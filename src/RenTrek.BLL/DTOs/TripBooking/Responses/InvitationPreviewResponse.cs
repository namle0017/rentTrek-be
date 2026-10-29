namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>
/// Thông tin công khai của link mời vào nhóm: chuyến đi, số người còn chờ và trạng thái khóa mã.
/// </summary>
public sealed class InvitationPreviewResponse
{
    public Guid OrderId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset TripStartAt { get; set; }
    public DateTimeOffset TripEndAt { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public int PendingCount { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsLocked { get; set; }
}
