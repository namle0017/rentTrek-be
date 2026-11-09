namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Phản hồi quản trị Trip của Admin, gồm sức chứa chuẩn, sức chứa vận hành có overflow và điều kiện mở bán.
/// </summary>
public sealed class AdminTripResponse
{
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public int? TourGroupMaxParticipants { get; set; }
    public Guid? RecurrenceRuleId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int MinParticipants { get; set; }
    public decimal PricePerPerson { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RowVersion { get; set; }
    public int GroupCount { get; set; }
    public int GroupOverflowSlots { get; set; }
    public int StandardGroupCapacity { get; set; }
    public int OperationalGroupCapacity { get; set; }
    public bool CanOpenForBooking { get; set; }
    public string? OpenReadinessWarning { get; set; }
    public bool ProvidedItemsNeedsRecalc { get; set; }
    public IReadOnlyList<AdminTripGroupSummaryResponse> Groups { get; set; } = [];
}
