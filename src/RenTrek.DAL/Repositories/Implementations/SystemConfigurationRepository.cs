using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho cấu hình hệ thống qua EF Core.
/// </summary>
public class SystemConfigurationRepository : GenericRepository<SystemConfiguration>, ISystemConfigurationRepository
{
    public SystemConfigurationRepository(AppDbContext context) : base(context)
    {
    }

    public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .Where(c => c.ConfigKey == key && !c.IsDeleted)
            .Select(c => (string?)c.ConfigValue)
            .FirstOrDefaultAsync(cancellationToken);
}
