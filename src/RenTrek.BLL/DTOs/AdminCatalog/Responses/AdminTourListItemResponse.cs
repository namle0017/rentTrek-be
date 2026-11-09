namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

public sealed class AdminTourListItemResponse
{
    public Guid TourId { get; set; }
    public Guid TourFamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string MainLocationName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public decimal BasePrice { get; set; }
    public int Version { get; set; }
    public bool IsItineraryLocked { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public int UpcomingTripsCount { get; set; }
}
