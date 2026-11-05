using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class AuditLogRepository : GenericRepository<AuditLog>, IAuditLogRepository
{
    private static readonly Expression<Func<AuditLog, AuditLogRow>> ToRow = x => new AuditLogRow
    {
        AuditLogId = x.AuditLogId,
        ActorUserId = x.ActorUserId,
        ActorFullName = x.ActorUser == null ? null : x.ActorUser.FullName,
        Action = x.Action,
        EntityType = x.EntityType,
        EntityId = x.EntityId,
        Details = x.Details,
        CreatedAt = x.CreatedAt
    };

    public AuditLogRepository(AppDbContext context) : base(context)
    {
    }

    public Task<(IReadOnlyList<AuditLogRow> Items, int TotalCount)> GetPagedAsync(
        AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();
        if (filter.RelatedUserId.HasValue)
            query = query.Where(x => x.ActorUserId == filter.RelatedUserId
                || (x.EntityType == "Account" && x.EntityId == filter.RelatedUserId));
        if (filter.ActorUserId.HasValue) query = query.Where(x => x.ActorUserId == filter.ActorUserId);
        if (filter.EntityId.HasValue) query = query.Where(x => x.EntityId == filter.EntityId);
        if (filter.EntityType != null) query = query.Where(x => x.EntityType == filter.EntityType);
        if (filter.Action != null) query = query.Where(x => x.Action == filter.Action);
        if (filter.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= filter.FromDate);
        if (filter.ToDate.HasValue) query = query.Where(x => x.CreatedAt <= filter.ToDate);

        return query
            .SortBy(filter.SortBy, filter.IsDescending, nameof(AuditLog.CreatedAt))
            .ToPageAsync(filter.PageNumber, filter.PageSize, ToRow, cancellationToken);
    }

    public Task<List<AuditLogRow>> GetForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var participants = _context.Set<BookingParticipant>();
        return _dbSet.AsNoTracking()
            .Where(x => x.EntityId == orderId || (x.EntityType == "BookingParticipant"
                && participants.Any(p => p.BookingOrderId == orderId && p.ParticipantId == x.EntityId)))
            .OrderByDescending(x => x.CreatedAt)
            .Select(ToRow)
            .ToListAsync(cancellationToken);
    }
}
