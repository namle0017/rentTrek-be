namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu sửa thông tin nhóm trong chuyến đi (9.3).
/// </summary>
public sealed class UpdateTripGroupRequest
{
    public string GroupName { get; set; } = string.Empty;
    public int MaxParticipants { get; set; } = 10;
    public Guid? GuideUserId { get; set; }
}
