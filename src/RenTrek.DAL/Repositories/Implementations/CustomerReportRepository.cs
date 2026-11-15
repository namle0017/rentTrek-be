using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <inheritdoc cref="ICustomerReportRepository"/>
public sealed class CustomerReportRepository(AppDbContext context) : ICustomerReportRepository
{
    public Task AddAsync(CustomerReport report, CancellationToken cancellationToken = default)
        => context.CustomerReports.AddAsync(report, cancellationToken).AsTask();

    public Task<CustomerReport?> GetDetailAsync(Guid customerReportId, CancellationToken cancellationToken = default)
        => WithGraph(context.CustomerReports)
            .SingleOrDefaultAsync(x => x.CustomerReportId == customerReportId, cancellationToken);

    public async Task<(IReadOnlyList<CustomerReport> Items, int TotalItems)> SearchAsync(
        string? sourceType,
        string? status,
        string? keyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.CustomerReports.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(sourceType))
            query = query.Where(x => x.SourceType == sourceType);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Order.OrderCode, pattern)
                || EF.Functions.ILike(x.ReporterUser.FullName, pattern)
                || EF.Functions.ILike(x.ReporterUser.Email, pattern)
                || (x.ReporterUser.Phone != null && EF.Functions.ILike(x.ReporterUser.Phone, pattern))
                || (x.SubjectLabel != null && EF.Functions.ILike(x.SubjectLabel, pattern)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await WithGraph(query)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken cancellationToken = default)
        => await context.CustomerReports
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<CustomerReport>> GetByReporterAsync(
        Guid reporterUserId,
        Guid? orderId,
        CancellationToken cancellationToken = default)
    {
        var query = context.CustomerReports.AsNoTracking().Where(x => x.ReporterUserId == reporterUserId);
        if (orderId is { } id)
            query = query.Where(x => x.OrderId == id);
        return await WithGraph(query).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Order?> GetOrderForReportAsync(Guid orderId, CancellationToken cancellationToken = default)
        => context.Orders
            .AsNoTracking()
            .Include(x => x.TripBookingOrder).ThenInclude(x => x.Trip).ThenInclude(x => x.Tour)
            .Include(x => x.TripBookingOrder).ThenInclude(x => x.Participants)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.OrderId == orderId && !x.IsDeleted, cancellationToken);

    private static IQueryable<CustomerReport> WithGraph(IQueryable<CustomerReport> query)
        => query
            .Include(x => x.ReporterUser)
            .Include(x => x.HandledByUser)
            .Include(x => x.Order).ThenInclude(x => x.TripBookingOrder).ThenInclude(x => x.Trip).ThenInclude(x => x.Tour)
            .AsSplitQuery();
}
