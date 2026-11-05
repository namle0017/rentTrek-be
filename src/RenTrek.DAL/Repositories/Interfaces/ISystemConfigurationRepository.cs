using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ISystemConfigurationRepository : IGenericRepository<SystemConfiguration>
{
    /// <summary>Giá trị cấu hình (chưa xoá) theo khoá; null nếu chưa cấu hình.</summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Mọi cấu hình chưa xoá, theo khoá tăng dần.</summary>
    Task<List<SystemConfiguration>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Cấu hình chưa xoá theo khoá (tracking để cập nhật).</summary>
    Task<SystemConfiguration?> GetForUpdateAsync(string key, CancellationToken cancellationToken = default);
}
