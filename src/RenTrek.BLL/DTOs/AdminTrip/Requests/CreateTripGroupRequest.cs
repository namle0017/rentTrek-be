namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu tạo nhóm mới trong chuyến đi (9.2).
/// </summary>
public sealed class CreateTripGroupRequest
{
    public string GroupName { get; set; } = string.Empty;
    public int MaxParticipants { get; set; } = 10;
    public Guid? GuideUserId { get; set; }
}
