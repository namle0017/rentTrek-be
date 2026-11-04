namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin chi tiết tổng quan của chuyến đi tại màn O10 (8.2, 8.3, 8.4).
/// </summary>
public sealed class AdminTripDetailResponse
{
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public int TourVersion { get; set; }
    public Guid? RecurrenceRuleId { get; set; }
    public Guid RefundPolicyId { get; set; }
    public string RefundPolicyName { get; set; } = string.Empty;
    public Guid? ManagedByUserId { get; set; }
    public string? ManagerFullName { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int SoldSlots { get; set; }
    public int RemainingSlots => Math.Max(0, TotalSlots - SoldSlots);
    public int MinParticipants { get; set; }
    public decimal PricePerPerson { get; set; }
    public decimal EstimatedRevenue => SoldSlots * PricePerPerson;
    public string Source { get; set; } = "Manual";
    public string Status { get; set; } = string.Empty;
    public int RowVersion { get; set; }
    public int GroupsCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
