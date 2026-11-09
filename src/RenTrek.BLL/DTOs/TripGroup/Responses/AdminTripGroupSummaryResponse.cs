namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Tóm tắt một TripGroup trong phản hồi quản trị Trip của Admin.
/// </summary>
public sealed class AdminTripGroupSummaryResponse
{
    public Guid TripGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int MaxParticipants { get; set; }
    public int OverflowSlots { get; set; }
    public int CurrentParticipants { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? GuideUserId { get; set; }
    public string? GuideName { get; set; }
}
