namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Hàng hành khách kèm từ điển trạng thái điểm danh theo từng hoạt động trong ma trận O14 (11.1).
/// </summary>
public sealed class AttendanceMatrixParticipantRowResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public IDictionary<string, AttendanceCellValueResponse> Attendances { get; set; } =
        new Dictionary<string, AttendanceCellValueResponse>();
}
