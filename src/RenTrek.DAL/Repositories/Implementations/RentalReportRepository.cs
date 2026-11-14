using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalReportRepository : GenericRepository<RentalReport>, IRentalReportRepository
{
    private const string SubmittedStatus = "Submitted";

    public RentalReportRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<RentalReport> Items, int TotalItems)> SearchAsync(
        string? status,
        string? reportType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RentalReport>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(reportType))
        {
            query = query.Where(r => r.ReportType == reportType);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(r => r.RentalOrder).ThenInclude(o => o.Order).ThenInclude(o => o.CustomerUser)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReviewedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<RentalReport?> GetDetailAsync(Guid rentalReportId, CancellationToken cancellationToken = default)
        => _context.Set<RentalReport>()
            .Include(r => r.RentalOrder).ThenInclude(o => o.Order).ThenInclude(o => o.CustomerUser)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.RentalReportId == rentalReportId, cancellationToken);

    public Task<RentalReport?> GetOpenAsync(Guid rentalOrderId, string reportType, CancellationToken cancellationToken = default)
        => _context.Set<RentalReport>()
            .FirstOrDefaultAsync(r => r.RentalOrderId == rentalOrderId && r.ReportType == reportType && r.Status == SubmittedStatus, cancellationToken);

    public async Task<IReadOnlyList<RentalReport>> GetByOrderAsync(Guid rentalOrderId, CancellationToken cancellationToken = default)
        => await _context.Set<RentalReport>()
            .AsNoTracking()
            .Where(r => r.RentalOrderId == rentalOrderId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
}
