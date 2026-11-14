using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalEvidencePhotoRepository : GenericRepository<RentalEvidencePhoto>, IRentalEvidencePhotoRepository
{
    public RentalEvidencePhotoRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<RentalEvidencePhoto>> GetByOrderAsync(Guid rentalOrderId, CancellationToken cancellationToken = default)
        => await _context.Set<RentalEvidencePhoto>()
            .AsNoTracking()
            .Where(p => p.RentalOrderId == rentalOrderId && !p.IsDeleted)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Dictionary<Guid, int>> CountByAssignmentAsync(Guid rentalOrderId, string stage, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Set<RentalEvidencePhoto>()
            .AsNoTracking()
            .Where(p => p.RentalOrderId == rentalOrderId && p.Stage == stage && !p.IsDeleted && p.AssetAssignmentId != null)
            .GroupBy(p => p.AssetAssignmentId)
            .Select(g => new { AssignmentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.AssignmentId.GetValueOrDefault(), x => x.Count);
    }
}
