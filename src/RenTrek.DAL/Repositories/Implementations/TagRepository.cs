using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TagRepository : GenericRepository<Tag>, ITagRepository
{
    public TagRepository(AppDbContext context) : base(context)
    {
    }

    public Task<List<Tag>> GetActiveAsync(CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public Task<int> CountActiveAsync(IReadOnlyCollection<Guid> tagIds, CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(t => tagIds.Contains(t.TagId) && !t.IsDeleted, cancellationToken);

    public Task<List<AdminTagRow>> SearchActiveAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(term)); }
        return query.OrderBy(x => x.Name).Select(x => new AdminTagRow
        {
            TagId = x.TagId,
            Name = x.Name,
            Slug = x.Slug,
            PostCount = x.PostTags.Count(t => !t.Post.IsDeleted)
        }).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => !x.IsDeleted && x.TagId != excludeId && x.Name.ToLower() == name.ToLower(), cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, Guid excludeId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => x.TagId != excludeId && x.Slug == slug, cancellationToken);
}
