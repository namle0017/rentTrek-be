namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một phiên bản trong họ tour (§6.24).</summary>
public sealed class AdminTourVersionResponse
{
    public Guid TourId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TripsCount { get; set; }
    public int UpcomingTripsCount { get; set; }
    public DateTime? ItineraryLockedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
