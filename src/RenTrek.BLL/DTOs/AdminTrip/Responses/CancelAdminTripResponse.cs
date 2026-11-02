namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Kết quả hủy chuyến đi và giải phóng tài nguyên/tạo hoàn tiền (8.10).
/// </summary>
public sealed class CancelAdminTripResponse
{
    public Guid TripId { get; set; }
    public int AffectedBookingsCount { get; set; }
    public int AffectedParticipantsCount { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public int ReleasedAssetsCount { get; set; }
}
