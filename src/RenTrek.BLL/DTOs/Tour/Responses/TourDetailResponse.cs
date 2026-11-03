namespace RenTrek.BLL.DTOs.Tour.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ tour detail.
/// </summary>
public class TourDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public string DurationFormatted => $"{DurationDays}N{DurationNights}Đ";
    public int DurationMinutes { get; set; }
    public decimal BasePrice { get; set; }
    public decimal EquipmentDeposit { get; set; } = 500000m; // Cọc thiết bị: 500.000đ (hoàn 100%)
    public int InspectionRate { get; set; } = 96;           // Đồ kiểm định 96%
    public int? GroupMaxParticipants { get; set; }
    public string Status { get; set; } = string.Empty;

    // Phân loại & Địa điểm
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string LocationAddress { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string CoordinatesFormatted => $"{LocationName} • {Latitude:F4}° N, {Longitude:F4}° E";

    public double Rating { get; set; } = 4.9;
    public int TotalReviews { get; set; } = 128;

    // Chi tiết kèm theo đúng bố cục Figma
    public List<TourImageResponse> Images { get; set; } = [];
    public List<TourActivityResponse> Activities { get; set; } = [];
    public List<TourItemResponse> ProvidedItems { get; set; } = [];
    public List<TourItemResponse> PreparationItems { get; set; } = [];
    public List<TripSummaryResponse> AvailableTrips { get; set; } = [];
}
