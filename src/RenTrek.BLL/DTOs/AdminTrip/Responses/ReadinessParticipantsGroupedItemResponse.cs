namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Tiêu chí 2: Toàn bộ hành khách đã được phân nhóm (8.5).
/// </summary>
public sealed class ReadinessParticipantsGroupedItemResponse
{
    public bool Passed { get; set; }
    public int UnassignedCount { get; set; }
}
