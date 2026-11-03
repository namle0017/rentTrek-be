namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Phân bổ danh sách participant của một booking vào một TripGroup cụ thể khi tách booking.
/// </summary>
public sealed class ParticipantGroupAssignmentRequest
{
    public Guid TripGroupId { get; set; }
    public List<Guid> ParticipantIds { get; set; } = [];
}
