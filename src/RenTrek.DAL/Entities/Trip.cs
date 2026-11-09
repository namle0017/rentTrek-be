namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể trip trong mô hình dữ liệu RenTrek.
/// </summary>
public sealed class Trip
{
    public Guid TripId { get; set; }
    public Guid TourId { get; set; }
    public Guid? RecurrenceRuleId { get; set; }
    public Guid RefundPolicyId { get; set; }
    public Guid ManagedByUserId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal PricePerPerson { get; set; }
    public int MinParticipants { get; set; }
    public int RowVersion { get; set; }
    public bool ProvidedItemsNeedsRecalc { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Không có cột trong DB; TourRepository tính từ booking đang chiếm chỗ
    public int BookedSlots { get; set; }

    public Tour Tour { get; set; } = null!;
    public ICollection<BookingParticipant> BookingParticipants { get; set; } = [];
    public Account ManagedByUser { get; set; } = null!;
    public TourRecurrenceRule? RecurrenceRule { get; set; }
    public RefundPolicy RefundPolicy { get; set; } = null!;
    public ICollection<TripBookingOrder> TripBookingOrders { get; set; } = [];
    public ICollection<TripGroup> TripGroups { get; set; } = [];
    public ICollection<TripProvidedItem> TripProvidedItems { get; set; } = [];
}
