namespace RenTrek.DAL.Models;

/// <summary>
/// Một phiên bản trong họ tour cho drawer lịch sử phiên bản (O07).
/// </summary>
public class TourVersionRow
{
    public Guid TourId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ItineraryLockedAt { get; set; }
    public int TripsCount { get; set; }
    public int UpcomingTripsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
