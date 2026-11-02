namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin một cột nhóm trong bảng Kanban chia nhóm O12 (9.1, 9.2, 9.3).
/// </summary>
public sealed class KanbanGroupColumnResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int MaxParticipants { get; set; }
    public int CurrentCount { get; set; }
    public bool HasScheduleWarning { get; set; }
    public string? ScheduleWarningMessage { get; set; }
    public KanbanGuideInfoResponse? Guide { get; set; }
    public IReadOnlyList<KanbanGroupParticipantItemResponse> Participants { get; set; } = [];
}
