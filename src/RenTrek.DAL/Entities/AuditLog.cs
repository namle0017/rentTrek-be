namespace RenTrek.DAL.Entities;

public class AuditLog
{
    public Guid AuditLogId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string Details { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Account? ActorUser { get; set; }
}
