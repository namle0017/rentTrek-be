using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context)
    {
    }

    public Task<List<Category>> SearchActiveAsync(string? search, string? categoryType, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(term)); }
        if (!string.IsNullOrWhiteSpace(categoryType)) query = query.Where(x => x.CategoryType.ToLower() == categoryType.ToLower());
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => !x.IsDeleted && x.Id != excludeId && x.Name.ToLower() == name.ToLower(), cancellationToken);
}
