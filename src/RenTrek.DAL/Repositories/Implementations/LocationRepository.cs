using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class LocationRepository : GenericRepository<Location>, ILocationRepository
{
    public LocationRepository(AppDbContext context) : base(context)
    {
    }

    public Task<List<AdminLocationRow>> SearchActiveAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(term)); }
        return query.OrderBy(x => x.Name).Select(x => new AdminLocationRow
        {
            Id = x.Id,
            Name = x.Name,
            Address = x.Address,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
            TourCount = x.Tours.Count(t => !t.IsDeleted),
            CreatedAt = x.CreatedAt
        }).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(x => !x.IsDeleted && x.Id != excludeId && x.Name.ToLower() == name.ToLower(), cancellationToken);
}
