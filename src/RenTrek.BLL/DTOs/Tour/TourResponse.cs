namespace RenTrek.BLL.DTOs.Tour;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ tour.
/// </summary>
public class TourResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public string DurationFormatted => $"{DurationDays} ngày {DurationNights} đêm";
    public string DifficultyLevel { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;

    // Các trường đặc trưng hiển thị trên Figma Tour Card (Node 92:2)
    public string BadgeStatus { get; set; } = "Đang mở"; // "Đang mở" | "Hết hạn"
    public string? DepartureDateRange { get; set; }      // Ví dụ: "28 – 29/09/2026"
    public string? SlotStatusText { get; set; }          // Ví dụ: "Còn 4 chỗ", "Đã khởi hành", "Đã kết thúc"
    public int? AvailableSlots { get; set; }
    public DateTime? NextDepartureDate { get; set; }
    public double Rating { get; set; } = 4.9;
    public int TotalReviews { get; set; } = 128;
}
