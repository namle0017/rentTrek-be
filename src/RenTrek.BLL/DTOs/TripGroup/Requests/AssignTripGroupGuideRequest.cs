namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Gán hoặc bỏ hướng dẫn viên cho một TripGroup khi còn ở trạng thái Draft.
/// </summary>
public sealed class AssignTripGroupGuideRequest
{
    public Guid? GuideUserId { get; set; }
    public int? ExpectedTripRowVersion { get; set; }
}
