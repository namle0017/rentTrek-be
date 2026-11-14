using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class RentalPolicyRepository : GenericRepository<RentalPolicy>, IRentalPolicyRepository
{
    private const string ActiveStatus = "Active";

    public RentalPolicyRepository(AppDbContext context) : base(context)
    {
    }

    public Task<RentalPolicy?> GetActiveWithTiersAsync(CancellationToken cancellationToken = default)
        => _context.Set<RentalPolicy>()
            .AsNoTracking()
            .Include(p => p.Tiers)
            .Where(p => !p.IsDeleted && p.Status == ActiveStatus)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RentalPolicy?> GetWithTiersAsync(Guid rentalPolicyId, CancellationToken cancellationToken = default)
        => _context.Set<RentalPolicy>()
            .AsNoTracking()
            .Include(p => p.Tiers)
            .FirstOrDefaultAsync(p => p.RentalPolicyId == rentalPolicyId, cancellationToken);

    public Task<RentalPolicy?> GetForUpdateAsync(Guid rentalPolicyId, CancellationToken cancellationToken = default)
        => _context.Set<RentalPolicy>()
            .Include(p => p.Tiers)
            .FirstOrDefaultAsync(p => p.RentalPolicyId == rentalPolicyId && !p.IsDeleted, cancellationToken);

    public Task<RentalPolicy?> GetActiveForUpdateAsync(CancellationToken cancellationToken = default)
        => _context.Set<RentalPolicy>()
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.Status == ActiveStatus, cancellationToken);

    public async Task<IReadOnlyList<RentalPolicy>> GetAllWithTiersAsync(CancellationToken cancellationToken = default)
        => await _context.Set<RentalPolicy>()
            .AsNoTracking()
            .Include(p => p.Tiers)
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> GetMaxVersionAsync(Guid policyFamilyId, CancellationToken cancellationToken = default)
        => await _context.Set<RentalPolicy>()
            .Where(p => p.PolicyFamilyId == policyFamilyId)
            .Select(p => (int?)p.Version)
            .MaxAsync(cancellationToken) ?? 0;
}
