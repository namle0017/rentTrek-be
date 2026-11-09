namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Dữ liệu ma trận điểm danh 2 chiều của chuyến đi tại màn O14 (11.1).
/// </summary>
public sealed class TripAttendanceMatrixResponse
{
    public IReadOnlyList<AttendanceMatrixActivityColumnResponse> Activities { get; set; } = [];
    public IReadOnlyList<AttendanceMatrixParticipantRowResponse> Participants { get; set; } = [];
}
