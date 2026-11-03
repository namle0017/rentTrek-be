namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Kết quả thực thi sinh Trip từ lịch lặp.
/// </summary>
public sealed class GenerateRecurringTripsResponse
{
    public int GeneratedTripsCount { get; set; }
    public int GeneratedGroupsCount { get; set; }
    public int SkippedExistingCount { get; set; }
    public IReadOnlyList<AdminTripResponse> GeneratedTrips { get; set; } = [];
    public IReadOnlyList<RecurrenceGenerationWarningResponse> Warnings { get; set; } = [];
}
