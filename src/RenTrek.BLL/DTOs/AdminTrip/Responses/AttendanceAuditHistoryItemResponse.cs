namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Một bản ghi nhật ký kiểm toán chỉnh sửa điểm danh của chuyến đi (11.3).
/// </summary>
public sealed class AttendanceAuditHistoryItemResponse
{
    public Guid AuditLogId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
