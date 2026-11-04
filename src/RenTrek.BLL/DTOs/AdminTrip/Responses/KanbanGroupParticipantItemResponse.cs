namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thẻ hành khách đã nằm trong một nhóm trên bảng Kanban O12 (9.1).
/// </summary>
public sealed class KanbanGroupParticipantItemResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Guid BookingOrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
}
