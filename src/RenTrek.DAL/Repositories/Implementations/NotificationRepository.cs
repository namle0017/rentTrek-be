using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu thông báo qua EF Core.
/// </summary>
public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context)
    {
    }

    public Task<bool> ExistsByEventKeyAsync(
        Guid recipientUserId,
        string eventKey,
        CancellationToken cancellationToken = default)
    {
        var hasPending = _context.ChangeTracker.Entries<Notification>()
            .Any(e => e.State == EntityState.Added &&
                      e.Entity.RecipientUserId == recipientUserId &&
                      e.Entity.EventKey == eventKey);
        if (hasPending)
            return Task.FromResult(true);

        return _dbSet.AnyAsync(
            n => n.RecipientUserId == recipientUserId && n.EventKey == eventKey,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedForUserAsync(
        Guid recipientUserId,
        bool? unreadOnly,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId);

        if (unreadOnly == true)
            query = query.Where(n => n.ReadAt == null);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(
            n => n.RecipientUserId == recipientUserId && n.ReadAt == null,
            cancellationToken);

    public Task<Notification?> GetOwnedForUpdateAsync(
        Guid notificationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
        => _dbSet.SingleOrDefaultAsync(
            n => n.NotificationId == notificationId && n.RecipientUserId == recipientUserId,
            cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadOwnedForUpdateAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(n => n.RecipientUserId == recipientUserId && n.ReadAt == null)
            .ToListAsync(cancellationToken);
}
