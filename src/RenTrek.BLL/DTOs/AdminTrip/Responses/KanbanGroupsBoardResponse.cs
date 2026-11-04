namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Dữ liệu toàn bộ bảng Kanban chia nhóm chuyến đi tại màn O12 (9.1).
/// </summary>
public sealed class KanbanGroupsBoardResponse
{
    public IReadOnlyList<KanbanUnassignedParticipantItemResponse> UnassignedParticipants { get; set; } = [];
    public IReadOnlyList<KanbanGroupColumnResponse> Groups { get; set; } = [];
}
