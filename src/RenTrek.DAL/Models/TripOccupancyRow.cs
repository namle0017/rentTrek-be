namespace RenTrek.DAL.Models;

/// <summary>
/// Số chỗ đã đặt của một chuyến cho báo cáo lấp đầy.
/// </summary>
public class TripOccupancyRow
{
    public Guid TripId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public int TotalSlots { get; set; }

    /// <summary>Tổng người của đơn Confirmed / InFulfillment / Completed.</summary>
    public int BookedCount { get; set; }
}
