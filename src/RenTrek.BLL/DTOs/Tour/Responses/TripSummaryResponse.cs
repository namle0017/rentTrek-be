namespace RenTrek.BLL.DTOs.Tour.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ trip summary.
/// </summary>
public class TripSummaryResponse
{
    public Guid Id { get; set; }
    /// <summary>
    /// Phiên bản tour tạo ra chuyến này; khách đi theo lịch trình của bản đó.
    /// Khác id tour đang xem nghĩa là chuyến của phiên bản trước cùng họ, còn bán tiếp;
    /// FE mở GET /tours/{TourId} để xem đúng lịch trình của chuyến.
    /// </summary>
    public Guid TourId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string DateRangeFormatted => $"{StartAt:dd/MM/yyyy}";
    public string TripOptionTitle => $"Đợt {StartAt:dd/MM/yyyy} (Còn {AvailableSlots} chỗ)";
    public int TotalSlots { get; set; }
    public int BookedSlots { get; set; }
    public int AvailableSlots => Math.Max(0, TotalSlots - BookedSlots);
    public int MinParticipants { get; set; }
    public decimal PricePerPerson { get; set; }
    public string Status { get; set; } = string.Empty;
}
