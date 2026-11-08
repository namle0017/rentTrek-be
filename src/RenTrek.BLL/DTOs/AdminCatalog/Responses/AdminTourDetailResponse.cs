namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Tab Thông tin chung màn O06 (§6.3); IsItineraryLocked = đã có khách đặt, cấm sửa lịch trình / đồ.</summary>
public sealed class AdminTourDetailResponse
{
    public Guid TourId { get; set; }
    public Guid TourFamilyId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Guid MainLocationId { get; set; }
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public int DurationMinutes { get; set; }
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsItineraryLocked { get; set; }
    public DateTime? ItineraryLockedAt { get; set; }
}
