namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng nhật ký audit kèm tên người thực hiện. Details là JSON thô, BLL tự parse.
/// </summary>
public class AuditLogRow
{
    public Guid AuditLogId { get; set; }
    public Guid? ActorUserId { get; set; }

    /// <summary>Null khi log do hệ thống tạo (không có người thực hiện).</summary>
    public string? ActorFullName { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
