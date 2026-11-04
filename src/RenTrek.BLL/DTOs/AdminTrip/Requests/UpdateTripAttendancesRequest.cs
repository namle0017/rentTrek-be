namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu cập nhật điểm danh hoạt động cho chuyến đi (11.2).
/// </summary>
public sealed class UpdateTripAttendancesRequest
{
    public Guid ActivityId { get; set; }
    public IReadOnlyList<TripAttendanceItemRequest> Records { get; set; } = [];
}
