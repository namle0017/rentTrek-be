namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị trips.status trong luồng vận hành Trip và TripGroup.
/// </summary>
public static class TripStatuses
{
    public const string Draft = "Draft";
    public const string Open = "Open";
    public const string ClosedForBooking = "ClosedForBooking";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}
