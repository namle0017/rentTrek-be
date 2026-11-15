using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho account qua EF Core.
/// </summary>
public class AccountRepository : GenericRepository<Account>, IAccountRepository
{
    public AccountRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Account?> GetByEmailWithRoleAsync(string email, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(u => u.Role)
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public async Task<Account?> GetByIdWithRoleAsync(Guid userId, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var query = tracking ? _dbSet : _dbSet.AsNoTracking();
        return await query
            .Include(u => u.Role)
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        // Gồm cả tài khoản đã xoá mềm: email vẫn chiếm unique index.
        return await _dbSet.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public Task<(IReadOnlyList<AdminUserRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminUserFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(search) || x.Email.ToLower().Contains(search)
                || (x.Phone != null && x.Phone.Contains(search)) || (x.Profile != null && x.Profile.UserCode.ToLower().Contains(search)));
        }
        if (filter.Role != null) query = query.Where(x => x.Role.RoleName == filter.Role);
        if (filter.Status != null) query = query.Where(x => x.Status == filter.Status);

        return query
            .SortBy(filter.SortBy, filter.IsDescending, nameof(Account.CreatedAt), nameof(Account.FullName),
                nameof(Account.Email), nameof(Account.Status))
            .ToPageAsync(filter.PageNumber, filter.PageSize, ToAdminRow, cancellationToken);
    }

    public Task<AdminUserRow?> GetAdminRowAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().Where(x => x.UserId == userId).Select(ToAdminRow).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminUserRow>> GetByRolesAsync(
        IReadOnlyCollection<string> roleNames, string status, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Where(x => x.Status == status && roleNames.Contains(x.Role.RoleName))
            .OrderBy(x => x.FullName)
            .Select(ToAdminRow)
            .ToListAsync(cancellationToken);

    public async Task<AccountActivityRow> GetActivityAsync(Guid userId, CancellationToken cancellationToken = default)
        => new()
        {
            OrderCount = await _context.Set<Order>().CountAsync(x => x.CustomerUserId == userId, cancellationToken),
            ManagedTripCount = await _context.Set<Trip>().CountAsync(x => x.ManagedByUserId == userId, cancellationToken),
            GuidedTripCount = await _context.Set<TripGroup>().Where(x => x.GuideUserId == userId)
                .Select(x => x.TripId).Distinct().CountAsync(cancellationToken)
        };

    private static readonly Expression<Func<Account, AdminUserRow>> ToAdminRow = x => new AdminUserRow
    {
        UserId = x.UserId,
        UserCode = x.Profile == null ? null : x.Profile.UserCode,
        Email = x.Email,
        FullName = x.FullName,
        Phone = x.Phone,
        RoleName = x.Role.RoleName,
        Status = x.Status,
        AvatarUrl = x.Profile == null ? null : x.Profile.AvatarUrl,
        LastLoginAt = x.LastLoginAt,
        CreatedAt = x.CreatedAt
    };
}
