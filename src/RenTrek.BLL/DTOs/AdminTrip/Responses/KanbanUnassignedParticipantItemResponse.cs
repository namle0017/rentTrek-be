namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thẻ hành khách chưa xếp nhóm trên bảng Kanban O12 (9.1).
/// </summary>
public sealed class KanbanUnassignedParticipantItemResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Guid BookingOrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string GroupingPreference { get; set; } = string.Empty;
    public bool IsSplit { get; set; }
    public bool HasAccount { get; set; }
}
