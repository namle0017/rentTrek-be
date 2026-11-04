namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin Hướng dẫn viên phụ trách nhóm trên cột Kanban O12 (9.1).
/// </summary>
public sealed class KanbanGuideInfoResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
