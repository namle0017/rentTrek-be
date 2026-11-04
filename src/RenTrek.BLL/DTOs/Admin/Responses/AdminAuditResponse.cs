namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminAuditResponse(Guid AuditLogId, Guid? ActorUserId, string? ActorFullName, string Action, string EntityType, Guid EntityId, System.Text.Json.JsonElement Details, DateTime CreatedAt);
