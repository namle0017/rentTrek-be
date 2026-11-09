namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Kết quả cập nhật điểm danh hoạt động tại màn O14 (11.2).
/// </summary>
public sealed class UpdateTripAttendancesResponse
{
    public Guid ActivityId { get; set; }
    public int UpdatedCount { get; set; }
}
