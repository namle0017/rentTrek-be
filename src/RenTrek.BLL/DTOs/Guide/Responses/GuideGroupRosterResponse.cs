namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Chi tiết nhóm và danh sách khách phụ trách của Guide tại màn G02 (24.2).
/// </summary>
public sealed class GuideGroupRosterResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid TripId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public GuideCurrentActivityResponse? CurrentActivity { get; set; }
    public IReadOnlyList<GuideGroupParticipantItemResponse> Participants { get; set; } = [];
}
