using System.Text.Json;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Payments;

internal static class PaymentEvents
{
    public static Task AuditAsync(IUnitOfWork uow, TimeProvider clock, string action, string entityType, Guid id, Guid? actor, object details, CancellationToken ct)
        => uow.Repository<AuditLog>().AddAsync(new AuditLog { AuditLogId = Guid.NewGuid(), Action = action, EntityType = entityType, EntityId = id, ActorUserId = actor, Details = JsonSerializer.Serialize(details), CreatedAt = clock.GetUtcNow().UtcDateTime }, ct);
    public static Task NotifyAsync(IUnitOfWork uow, TimeProvider clock, Guid userId, string type, Guid orderId, string message, CancellationToken ct)
        => uow.Repository<Notification>().AddAsync(new Notification { NotificationId = Guid.NewGuid(), RecipientUserId = userId, NotificationType = type, Title = message, Content = message, RelatedEntityType = "Order", RelatedEntityId = orderId, CreatedAt = clock.GetUtcNow().UtcDateTime }, ct);
}
