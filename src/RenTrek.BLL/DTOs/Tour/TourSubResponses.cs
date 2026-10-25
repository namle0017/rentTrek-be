namespace RenTrek.BLL.DTOs.Tour;

public class TourImageResponse
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}

public class TourActivityResponse
{
    public Guid Id { get; set; }
    public int DayNumber { get; set; }
    public string? DayTitle { get; set; }
    public TimeSpan StartTime { get; set; }
    public string TimeFormatted => StartTime.ToString(@"hh\:mm");
    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class TourItemResponse
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? QuantityBasis { get; set; }
    public int? Quantity { get; set; }
}

public class TripSummaryResponse
{
    public Guid Id { get; set; }
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
