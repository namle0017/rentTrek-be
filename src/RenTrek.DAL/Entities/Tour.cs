namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể tour trong mô hình dữ liệu RenTrek.
/// </summary>
public class Tour : BaseEntity
{
    public Guid TourFamilyId { get; set; } = Guid.NewGuid();
    public int Version { get; set; } = 1;
    public DateTime? ItineraryLockedAt { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Guid MainLocationId { get; set; }
    public Location MainLocation { get; set; } = null!;

    public Guid? CreatedByUserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Trung bình"; // Dễ, Trung bình, Thử thách, Khó
    public int DurationDays { get; set; } = 2;
    public int DurationNights { get; set; } = 1;
    public int DurationMinutes { get; set; } = 2880;
    public decimal BasePrice { get; set; }
    public decimal EquipmentDeposit { get; set; } = 500000m; // Tiền cọc thiết bị (Figma ghi 500.000đ hoàn 100%)
    public int InspectionRate { get; set; } = 96;           // Đồ kiểm định 96%
    public int? GroupMaxParticipants { get; set; }
    public string Status { get; set; } = "Active";          // Active (Đang mở), Expired (Hết hạn)

    // Navigations
    public ICollection<TourImage> Images { get; set; } = [];
    public ICollection<TourActivity> Activities { get; set; } = [];
    public ICollection<TourPreparationItem> PreparationItems { get; set; } = [];
    public ICollection<TourProvidedItem> ProvidedItems { get; set; } = [];
    public ICollection<Trip> Trips { get; set; } = [];
    public Account? CreatedByUser { get; set; }
    public ICollection<TourPost> TourPosts { get; set; } = [];
    public ICollection<TourRecurrenceRule> TourRecurrenceRules { get; set; } = [];
}
