namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Chuyển nguyên một booking sang một TripGroup đích khi còn Draft.
/// </summary>
public sealed class MoveBookingGroupRequest
{
    public Guid TargetGroupId { get; set; }
    public int? ExpectedTripRowVersion { get; set; }
    public string? Note { get; set; }
}
