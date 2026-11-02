namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin tóm tắt chuyến đi trong danh sách Table & Calendar View (8.1).
/// </summary>
public sealed class AdminTripItemResponse
{
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public int TourVersion { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int SoldSlots { get; set; }
    public int RemainingSlots => Math.Max(0, TotalSlots - SoldSlots);
    public int MinParticipants { get; set; }
    public decimal PricePerPerson { get; set; }
    public Guid? ManagedByUserId { get; set; }
    public string? ManagerFullName { get; set; }
    public string Source { get; set; } = "Manual";
    public string Status { get; set; } = string.Empty;
    public bool IsUnderCapacity => SoldSlots < MinParticipants;
}
