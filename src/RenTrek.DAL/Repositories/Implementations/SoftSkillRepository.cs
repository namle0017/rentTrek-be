using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class SoftSkillRepository : GenericRepository<SoftSkill>, ISoftSkillRepository
{
    public SoftSkillRepository(AppDbContext context) : base(context)
    {
    }

    public Task<List<SoftSkill>> GetActiveAsync(CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().Where(s => !s.IsDeleted).OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public Task<int> CountActiveAsync(IReadOnlyCollection<Guid> softSkillIds, CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(s => softSkillIds.Contains(s.SoftSkillId) && !s.IsDeleted, cancellationToken);

    public Task<List<SoftSkill>> SearchActiveAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(term)); }
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => !x.IsDeleted && x.SoftSkillId != excludeId && x.Name.ToLower() == name.ToLower(), cancellationToken);
}
