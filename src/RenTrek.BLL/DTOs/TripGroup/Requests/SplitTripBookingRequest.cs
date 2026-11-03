namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Tách một booking sang nhiều TripGroup kèm lý do bắt buộc.
/// </summary>
public sealed class SplitTripBookingRequest
{
    public string Reason { get; set; } = string.Empty;
    public int? ExpectedTripRowVersion { get; set; }
    public List<ParticipantGroupAssignmentRequest> Assignments { get; set; } = [];
}
